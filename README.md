# Smart API Gateway

A caching, rate-limited API gateway built with ASP.NET Core (C#). It sits in
front of any slow, rate-limited, or unreliable third-party API and manages
requests on its behalf — caching repeated responses, enforcing its own
outgoing rate limit, and tracking live performance metrics. This project uses
the official Clash Royale API as a real, working example third-party API to
demonstrate the pattern end-to-end, but the gateway's design is generic and
not tied to that API specifically.

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
2. **Caches responses** in memory so repeated requests for the same data
   within a configurable time window (currently 60 seconds) are served
   instantly, without ever touching the real API again.
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
Royale API: pass-through proxying, in-memory caching, rate limiting, and
metrics have all been confirmed working together with real request data.

## Roadmap

- [x] Project scaffold (ASP.NET Core Web API)
- [x] Basic pass-through proxy to a real third-party API
- [x] In-memory caching layer
- [x] Rate limiting on outgoing requests
- [x] Request logging / metrics (`GET /Metrics` endpoint)
- [ ] Redis-based caching instead of in-memory (for persistence across
      restarts and sharing across multiple running instances)
- [ ] Simple stats dashboard (visual version of `/Metrics`)

## Tech stack

- **ASP.NET Core (C#, .NET 8)** — the web framework
- **Swashbuckle / Swagger** — auto-generated interactive API docs (included
  by the default project template)
- Demonstrated against the **official Clash Royale API**
  (developer.clashroyale.com)

## Prerequisites

- **.NET 8 SDK** — check if already installed:
```bash
  dotnet --version
```
  If not installed, download from https://dotnet.microsoft.com/download
  (get the .NET 8.0 LTS SDK).
- **Visual Studio 2022** (Community edition is free) or another C#-capable
  editor (e.g. VS Code with the C# Dev Kit extension).
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

2. **Get your API key and current public IP.** Search "what is my ip" in a
   browser to find your current public IP address.

3. **Create the key** at developer.clashroyale.com → My Account → New Key,
   entering your current public IP in the "Allowed IP Addresses" field.
   Copy the generated key immediately (it's shown only once).

4. **Create your local settings file.** This project expects
   `appsettings.Development.json` at the project root (this file is
   git-ignored and never committed, since it holds your real key). Create it
   with this exact structure:
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

5. **Restore dependencies and run:**
```bash
   dotnet restore
   dotnet run
```
   Or, in Visual Studio, open `ClashRoyaleApiGateway.csproj` and click the
   green Run button.

6. **Test it.** Your browser should open automatically to the Swagger UI
   (typically `https://localhost:7088/swagger`). Try:
   - `GET /Player/{tag}` — enter any valid Clash Royale player tag (with or
     without the `#`, e.g. `YUP02GRQG`) to fetch that player's profile
     through the gateway.
   - Run the same request twice in a row — the second call should be
     noticeably faster, served from cache.
   - `GET /Metrics` — view live counts confirming caching and request
     tracking are working.

## Reusing this gateway for a *different* third-party API

The gateway's structure is intentionally simple to adapt to a different
upstream API. To point it at something other than Clash Royale:

1. **Update `appsettings.Development.json`** with the new API's base URL and
   your key/credentials for it.
2. **Modify `ClashRoyaleProxyService.cs`** (or copy it to a new file with a
   more generic name): change the endpoint path construction and any
   Clash-Royale-specific logic (like the `#` tag encoding) to match the new
   API's actual URL structure and authentication method (Bearer token,
   API key header, query parameter, etc. — varies by provider).
3. **Update or add a controller** exposing whatever endpoint shape makes
   sense for the new API (`PlayerController.cs` is a working example to copy
   from).
4. The **caching, rate limiting, and metrics logic are already generic** —
   `IMemoryCache`, `SimpleRateLimiter`, and `GatewayMetrics` have no
   Clash-Royale-specific code in them at all, so they work unchanged
   regardless of which upstream API you point the gateway at.

## Troubleshooting

**`No such host is known` / DNS resolution errors when calling the upstream
API:** this means your machine's configured DNS server can't resolve the
third-party API's domain name, even if general internet access works
perfectly fine otherwise (e.g. `ping google.com` succeeds). Diagnose by
testing against a known-reliable public DNS provider directly:
```bash
nslookup api.clashroyale.com 8.8.8.8
```
If this succeeds (returns a real IP address) while a plain `ping
api.clashroyale.com` fails, the problem is specifically your local/router
DNS, not the domain or your code. Fix by switching your network adapter's
DNS servers to a public provider — Google (`8.8.8.8`, `8.8.4.4`) or
Cloudflare (`1.1.1.1`, `1.0.0.1`) — via your OS's network adapter settings,
or on Windows via PowerShell (as Administrator):
```powershell
Set-DnsClientServerAddress -InterfaceAlias "Wi-Fi" -ServerAddresses ("8.8.8.8","8.8.4.4")
```

**`403 Forbidden` from the upstream API:** most third-party API keys
(including Clash Royale's) are restricted to a specific public IP address at
creation time. If your public IP has changed since the key was created —
common after switching networks, routers, countries, or ISPs, since most
home internet connections are assigned dynamic (changeable) public IPs —
the upstream API will reject requests from the new, unrecognized IP with a
403. Fix by checking your current public IP (search "what is my ip") and
updating the key's allowed IP on the API provider's developer portal (note:
Supercell's keys cannot be edited after creation — you'll need to generate a
new key with the current IP instead).

**JSON parsing errors on startup (e.g. `InvalidDataException` mentioning
`appsettings.Development.json`):** usually caused by a stray hidden
character (like a carriage return) accidentally pasted into a JSON string
value, most often the API key. Fix by deleting the file's content entirely
and retyping/re-pasting the JSON structure fresh, pasting the key value on
its own with no surrounding whitespace or line breaks.

## Repository structure

```
SmartApiGateway/
├── Controllers/
│   ├── PlayerController.cs    # GET /Player/{tag} - proxied player lookup
│   ├── MetricsController.cs   # GET /Metrics - gateway performance stats
│   └── TestController.cs      # Rate-limit testing endpoint
├── Services/
│   ├── ClashRoyaleProxyService.cs   # Upstream API calls + caching + rate limiting
│   ├── SimpleRateLimiter.cs         # Outgoing request rate limiting logic
│   └── GatewayMetrics.cs            # Thread-safe request/cache counters
├── Program.cs                 # App entry point, dependency injection setup
├── appsettings.json            # Base config (committed)
├── appsettings.Example.json    # Template showing required config shape (committed)
├── appsettings.Development.json # Real local config with your API key (NOT committed)
└── ARCHITECTURE.md             # Design decisions and reasoning
```