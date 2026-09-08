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
   `GatewayMetrics`, then checks **Redis** for an existing, still-fresh
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

6. The response is stored in **Redis** with a 60-second expiry, and returned
   to the controller, which returns it to the client.

7. At any point, a client can call `GET /Metrics` to see live counts of
   total requests, cache hits/misses, hit rate percentage, and rate-limited
   requests.

## Why each piece of technology was chosen

- **ASP.NET Core Web API** - a mature framework for building HTTP APIs in
  C#, with built-in dependency injection and automatic Swagger documentation.

- **Redis (via `StackExchange.Redis`)** - the caching layer. Originally
  built with ASP.NET Core's built-in `IMemoryCache`, which worked but had a
  real limitation: cached data lived only inside the app's own process
  memory, meaning it was wiped every time the app restarted, and could never
  be shared across multiple running instances of the gateway. Redis solves
  both problems by running as a separate, independent process that the app
  connects to as a client - cached data survives app restarts and could be
  shared across multiple gateway instances in a real deployment. This was
  confirmed directly: a player was cached, the app was fully restarted, and
  the same request afterward still returned a cache hit - something
  `IMemoryCache` could never do.

- **Docker (to run Redis)** - the simplest way to run a real Redis server
  locally without a native installation, using the official Redis image.

- **A custom `SimpleRateLimiter`** - written by hand so the actual mechanism
  (tracking recent outgoing requests, making new ones wait their turn) is
  fully understood, rather than being a black box from an imported package.

- **`Interlocked.Increment` in `GatewayMetrics`** - a thread-safe way to
  increase a shared counter, since regular `count++` is not safe when
  multiple requests update the same counter concurrently (a race condition).

- **`AddSingleton` for `GatewayMetrics` and the Redis connection,
  `AddHttpClient` for the proxy service** - `GatewayMetrics` and the Redis
  `IConnectionMultiplexer` need to be ONE shared instance across every
  request (Redis connections are also designed to be reused, not recreated
  per request), while the proxy service's `HttpClient` is managed
  per-request by the framework's `HttpClientFactory` pattern, avoiding known
  issues with manually creating `HttpClient` instances.

## Why cache at all?

Without caching, every request - even the exact same one repeated seconds
apart - would hit the real upstream API, defeating the purpose of a gateway.
Caching for a short window (60 seconds) means repeated lookups are served
instantly and never touch the real API.

## Why move from in-memory cache to Redis specifically?

Two concrete limitations of `IMemoryCache` in a production-style system:
1. It disappears on every app restart - a live production gateway restarts
   periodically (deployments, crashes, scaling events), and losing the
   entire cache each time defeats much of caching's benefit.
2. It cannot be shared across multiple running instances of the app - a
   real production gateway often runs several instances for load handling,
   and each would have its own disconnected cache with `IMemoryCache`,
   causing redundant upstream calls that Redis would eliminate.

## Why rate limit at all?

Third-party APIs enforce their own rate limits, and exceeding them can
result in throttling or a temporary ban. Enforcing a stricter limit inside
the gateway guarantees it never accidentally triggers the upstream's limit.

## Why track metrics?

A gateway that silently caches and rate-limits with no visibility into its
own behavior is hard to trust or debug. `/Metrics` answers practical
questions like "is caching actually helping?" - the kind of observability
real backend systems need.

## Real problems encountered, and what they taught

**DNS resolution failure (`No such host is known`)**: general internet
access worked, but `api.clashroyale.com` specifically could not be resolved
by the local network's DNS. Diagnosed by testing against Google's public DNS
directly (`nslookup api.clashroyale.com 8.8.8.8`), which succeeded - proving
the problem was local DNS, not the domain or the code. Fixed by switching
the machine's DNS servers to a public provider. General lesson: when one
specific domain fails while others work, test against an alternate DNS
provider to isolate whether the issue is DNS resolution or something else.

**`403 Forbidden` after switching countries/routers**: the API key's
IP allow-list no longer matched the new public IP assigned by the new
network. Normal behavior for IP-restricted keys, not a bug - the allowed IP
must be updated whenever the public IP changes.

**Docker connection error on first `docker run`**: Docker Desktop's engine
wasn't running yet, even though the `docker` CLI tool was installed. The
CLI and the background engine are separate - the engine must be fully
started before any `docker` command can succeed.

## Known limitations / honest tradeoffs

- **Single Redis instance, no persistence configuration beyond defaults** -
  a production setup would typically configure Redis persistence (RDB/AOF)
  and possibly clustering for high availability; this project uses Redis's
  default single-container setup, sufficient to demonstrate the pattern.
- **The rate limiter is a simple, hand-written implementation** - covers the
  core concept but isn't as feature-rich as production-grade rate-limiting
  middleware.
- **Single upstream API demonstrated** - the gateway's logic is currently
  shaped around the Clash Royale API specifically (player tags, endpoint
  structure). A fully generic multi-API gateway would need a more abstracted
  request-forwarding design.