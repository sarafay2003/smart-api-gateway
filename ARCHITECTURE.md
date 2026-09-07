# Architecture & Design Decisions

This document explains the reasoning behind the major technical choices in
this project, the exact flow of a request, and why each piece of technology
was chosen - written so it's useful for anyone reviewing the code, and for
future-me remembering why things are built this way.

## Execution flow: what happens on a request

1. A client sends `GET /Player/{tag}` to the gateway.

2. **`PlayerController`** receives the request and calls
   `ClashRoyaleProxyService.GetPlayerAsync(tag)` - the controller itself
   contains no real logic, it just delegates.

3. **`ClashRoyaleProxyService`** first records the request in
   `GatewayMetrics`, then checks `IMemoryCache` for an existing, still-fresh
   response for this exact player tag.
   - **Cache hit**: the cached response is returned immediately. No call to
     the real Clash Royale API happens at all.
   - **Cache miss**: the flow continues to the next step.

4. Before making the real outgoing call, the service calls
   `SimpleRateLimiter.WaitAsync()` - this blocks (waits) if too many requests
   have already gone out recently, ensuring the gateway never exceeds its own
   self-imposed limit to the upstream API.

5. Once cleared by the rate limiter, the service builds the real request
   (with the API key attached as a Bearer token) and sends it to the actual
   Clash Royale API.

6. The response is stored in the cache (so the next identical request within
   the cache window is served instantly), and returned to the controller,
   which returns it to the client.

7. At any point, a client can call `GET /Metrics` to see live counts of
   total requests, cache hits/misses, hit rate percentage, and rate-limited
   requests - this is served by `MetricsController` reading from the same
   shared `GatewayMetrics` instance every request updates.

## Why each piece of technology was chosen

- **ASP.NET Core Web API** - a mature, widely-used framework for building
  HTTP APIs in C#, with built-in dependency injection, middleware pipeline,
  and automatic Swagger/OpenAPI documentation generation - useful for
  testing endpoints without building a separate frontend first.

- **`IMemoryCache` (built into ASP.NET Core)** - the simplest way to cache
  data in a running app's own memory, with zero extra setup or external
  dependencies. Chosen for the first version specifically because it proves
  the caching *pattern* works before adding the complexity of an external
  cache store like Redis.

- **A custom `SimpleRateLimiter` instead of a third-party library** - written
  by hand deliberately, so the actual mechanism (tracking how many requests
  have gone out in a given time window, and making new requests wait their
  turn) is fully understood and explainable, rather than being a black box
  from an imported package.

- **`Interlocked.Increment` in `GatewayMetrics`** - a thread-safe way to
  increase a shared counter. Regular `count++` is not safe when multiple
  requests could be updating the same counter at the same time (a race
  condition) - `Interlocked` guarantees each increment completes atomically,
  so no counts are lost under concurrent load.

- **`AddSingleton` for `GatewayMetrics`, `AddHttpClient` for the proxy
  service** - two different dependency injection lifetimes, chosen
  deliberately: metrics need to be ONE shared instance across every request
  (so counts accumulate correctly), while the proxy service's `HttpClient`
  is managed per-request by the framework's `HttpClientFactory` pattern,
  which avoids known issues with manually creating `HttpClient` instances
  (socket exhaustion under load).

## Why cache at all?

Without caching, every single request - even the exact same one, repeated
seconds apart - would hit the real upstream API. This defeats the entire
purpose of a gateway: the point is to shield the upstream API from
redundant traffic. Caching for a short, configurable window (currently 60
seconds) means repeated lookups for the same data are served instantly and
never touch the real API at all.

## Why rate limit at all?

Third-party APIs like Clash Royale's enforce their own rate limits on the
server side, and exceeding them can result in throttling or a temporary ban
on the API key. By enforcing a stricter limit inside the gateway itself, the
gateway guarantees it will never accidentally trigger the upstream's limit,
regardless of how much traffic it receives on its own front end.

## Why track metrics?

A gateway that silently caches and rate-limits with no visibility into its
own behavior is hard to trust or debug in a real system. The `/Metrics`
endpoint answers practical questions like "is caching actually helping?" and
"are we getting close to being rate-limited?" - the kind of observability
real backend systems need.

## Two real problems encountered, and what they taught

**DNS resolution failure (`No such host is known`)**: general internet
access worked, but the specific domain `api.clashroyale.com` could not be
resolved by the local network's DNS server. Diagnosed by testing the same
domain against Google's public DNS (`nslookup api.clashroyale.com 8.8.8.8`),
which succeeded - proving the problem was the local DNS specifically, not
the domain, the code, or general connectivity. Fixed by manually switching
the machine's DNS servers to Google's public DNS. This is a useful general
debugging pattern: when one specific domain fails while others work, test
against an alternate DNS provider to isolate whether the problem is DNS
resolution or something else entirely.

**`403 Forbidden` after switching countries/routers**: the Clash Royale API
key was originally created with an allow-list of one specific public IP
address. Changing networks (new country, new router) meant a new public IP
was assigned by the new ISP, which no longer matched the key's allow-list.
This is a normal characteristic of IP-restricted API keys, not a bug -
whenever the public IP changes, the key's allowed IP must be updated to
match.

## Known limitations / honest tradeoffs

- **In-memory cache is not persistent or shared** - it resets on every app
  restart, and would not be shared across multiple running instances of the
  gateway. Redis is the natural next step to solve this (see roadmap).
- **The rate limiter is a simple, hand-written implementation** - it covers
  the core concept (waiting when a limit is reached) but is not as
  feature-rich as production-grade rate-limiting libraries or middleware.
- **Single upstream API demonstrated** - the gateway's logic is currently
  written specifically around the Clash Royale API's shape (player tags,
  specific endpoints). Making it truly generic for arbitrary third-party
  APIs would require a more abstracted request-forwarding design.