# Smart API Gateway

A caching, rate-limited API gateway built with ASP.NET Core (C#). It sits in
front of any slow, rate-limited, or unreliable third-party API and manages
requests on its behalf — caching repeated responses in Redis, enforcing its
own outgoing rate limit, and tracking live performance metrics. This project
uses the official Clash Royale API as a real, working example third-party
API to demonstrate the pattern end-to-end, but the gateway's design is
generic and not tied to that API specifically.

## The problem it solves

Third-party APIs are often rate-limited, slow, or have usage caps. If every
part of an application — or every user of it — calls a third-party API
directly, you risk hitting the provider's rate limits, paying for redundant
duplicate calls, and tying your whole system's reliability to someone else's
uptime. The standard fix used in real production systems is an API gateway:
your application talks only to your own service, which intelligently manages
the real calls to the third-party API on everyone's behalf, shielding both
your app and the upstream provider from unnecessary load.

## What it actually does

1. **Sits between client requests and a real third-party API** — clients call
   your gateway's own endpoints; the gateway is responsible for talking to
   the real upstream service.
2. **Caches responses in Redis**, a separate, independently-running cache
   server, so repeated requests for the same data within a configurable time
   window (currently 60 seconds) are served instantly, without ever touching
   the real API again — and the cache survives even if the gateway app
   itself restarts, since Redis runs as its own process.
3. **Enforces its own outgoing rate limit** on calls made *to* the upstream
   API, so the gateway can never accidentally get itself throttled or banned
   by the third-party provider, regardless of how much incoming traffic it
   receives.
4. **Tracks live metrics** — total requests, cache hits/misses, cache hit
   rate percentage, and rate-limited request count — exposed via a
   `GET /Metrics` endpoint, so the gateway's own behavior is observable, not
   a black box.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full reasoning behind every
design decision, the exact step-by-step request flow, and why each specific
technology was chosen over alternatives.

## Project status

Core pipeline is complete and tested end-to-end against the real Clash
Royale API: pass-through proxying, Redis-backed caching, rate limiting, and
metrics have all been confirmed working together with real request data —
including confirming the cache survives a full app restart.

## Roadmap

- [x] Project scaffold (ASP.NET Core Web API)
- [x] Basic pass-through proxy to a real third-party API
- [x] Caching layer
- [x] Rate limiting on outgoing requests
- [x] Request logging / metrics (`GET /Metrics` endpoint)
- [x] Redis-based caching instead of in-memory (persists across restarts)
- [ ] Simple stats dashboard (visual version of `/Metrics`)

## Tech stack

- **ASP.NET Core (C#, .NET 8)** — the web framework
- **Redis** (via `StackExchange.Redis`) — persistent, shared caching layer
- **Docker** — runs the local Redis instance
- **Swashbuckle / Swagger** — auto-generated interactive API docs
- Demonstrated against the **official Clash Royale API**
  (developer.clashroyale.com)

## Prerequisites

- **.NET 8 SDK** — check with `dotnet --version`. Install from
  https://dotnet.microsoft.com/download if missing.
- **Visual Studio 2022** (Community edition is free) or another C#-capable
  editor (e.g. VS Code with the C# Dev Kit extension).
- **Docker Desktop** — required to run Redis locally. Install from
  https://www.docker.com/products/docker-desktop.
- **An API key for whichever third-party API you're proxying.** This
  repository is demonstrated using the Clash Royale API — get a free key at
  developer.clashroyale.com. Note: this specific key is IP-restricted (see
  Troubleshooting below).

## Setup — running this exact project

1. **Clone the repository:**
```bash
   git clone https://github.com/sarafay2003/smart-api-gateway.git
   cd smart-api-gateway
```

2. **Start Redis** (via Docker Desktop, must be running first):
```bash
   docker run -d -p 6379:6379 --name redis-cache redis
```
   Confirm it's running:
```bash
   docker ps
```

3. **Get your API key and current public IP.** Search "what is my ip" in a
   browser to find your current public IP address.

4. **Create the key** at developer.clashroyale.com → My Account → New Key,
   entering your current public IP in the "Allowed IP Addresses" field.
   Copy the generated key immediately (it's shown only once).

5. **Create your local settings file.** This project expects
   `appsettings.Development.json` at the project root (this file is
   git-ignored and never committed, since it holds your real key):
```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "ThirdPartyApi": {
       "ApiKey": "your_real_key_here",
       "BaseUrl": "https://api.clashroyale.com/v1"
     }
   }
```
   (`appsettings.Example.json` in the repo shows this same structure without
   a real key, as a template.)

6. **Restore dependencies and run:**
```bash
   dotnet restore
   dotnet run
```
   Or, in Visual Studio, open `ClashRoyaleApiGateway.csproj` and click the
   green Run button.

7. **Test it.** Your browser should open automatically to the Swagger UI
   (typically `https://localhost:7088/swagger`). Try:
   - `GET /Player/{tag}` — enter any valid Clash Royale player tag (with or
     without the `#`, e.g. `YUP02GRQG`) to fetch that player's profile
     through the gateway.
   - Run the same request twice in a row — the second call should be
     noticeably faster, served from Redis.
   - Fully restart the app, then call the same tag again — it should still
     be a cache hit, proving the cache survived the restart.
   - `GET /Metrics` — view live counts confirming caching and request
     tracking are working.

## Reusing this gateway for a *different* third-party API

1. **Update `appsettings.Development.json`** with the new API's base URL and
   your key/credentials for it.
2. **Modify `ClashRoyaleProxyService.cs`** (or copy it to a new file with a
   more generic name): change the endpoint path construction and any
   Clash-Royale-specific logic (like the `#` tag encoding) to match the new
   API's actual URL structure and authentication method.
3. **Update or add a controller** exposing whatever endpoint shape makes
   sense for the new API (`PlayerController.cs` is a working example to copy
   from).
4. The **Redis caching, rate limiting, and metrics logic are already
   generic** — none of them contain Clash-Royale-specific code, so they work
   unchanged regardless of which upstream API you point the gateway at.

## Troubleshooting

**`No such host is known` / DNS resolution errors when calling the upstream
API:** your machine's DNS server can't resolve the third-party API's domain,
even if general internet access works fine. Diagnose:
```bash
nslookup api.clashroyale.com 8.8.8.8
```
If this succeeds while a plain `ping` fails, switch your network's DNS to a
public provider — Google (`8.8.8.8`, `8.8.4.4`) or Cloudflare (`1.1.1.1`,
`1.0.0.1`) — in your OS network settings, or via PowerShell (Administrator):
```powershell
Set-DnsClientServerAddress -InterfaceAlias "Wi-Fi" -ServerAddresses ("8.8.8.8","8.8.4.4")
```

**`403 Forbidden` from the upstream API:** your API key is IP-restricted,
and your public IP has likely changed since the key was created (common
after switching networks, routers, or countries, since most home IPs are
dynamic). Check your current IP ("what is my ip") and update the key's
allowed IP on the provider's developer portal (Supercell keys can't be
edited — generate a new one instead).

**`docker: error during connect` when starting Redis:** Docker Desktop
isn't running. Launch it from the Start menu and wait for it to fully
initialize (whale icon in the system tray) before retrying `docker run`.

**JSON parsing errors on startup mentioning `appsettings.Development.json`:**
usually a stray hidden character pasted into the API key. Delete the file's
content and retype the JSON fresh, pasting the key with no surrounding
whitespace or line breaks.

## Repository structure

```
SmartApiGateway/
├── Controllers/
│   ├── PlayerController.cs    # GET /Player/{tag} - proxied player lookup
│   ├── MetricsController.cs   # GET /Metrics - gateway performance stats
│   └── TestController.cs      # Rate-limit testing endpoint
├── Services/
│   ├── ClashRoyaleProxyService.cs   # Upstream API calls + Redis caching + rate limiting
│   ├── SimpleRateLimiter.cs         # Outgoing request rate limiting logic
│   └── GatewayMetrics.cs            # Thread-safe request/cache counters
├── Program.cs                 # App entry point, DI configuration, Redis connection setup
├── appsettings.json            # Base config (committed)
├── appsettings.Example.json    # Template showing required config shape (committed)
├── appsettings.Development.json # Real local config with your API key (NOT committed)
└── ARCHITECTURE.md             # Design decisions and reasoning
```