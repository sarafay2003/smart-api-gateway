# Clash Royale API Gateway

A caching, rate-limited API gateway built with ASP.NET Core, sitting in front
of the official Clash Royale API. Demonstrates a real backend pattern used by
companies to protect their systems from slow, rate-limited, or unreliable
third-party APIs.

## The problem it solves

Third-party APIs are often rate-limited, slow, or have usage caps. If every
part of an application (or every user) calls a third-party API directly,
you risk hitting rate limits, paying for redundant calls, and exposing your
whole system's reliability to someone else's uptime. The standard fix is an
API gateway: your application talks to your own service, which intelligently
manages the real calls to the third-party API on everyone's behalf.

## What it does

1. Sits between client requests and the real Clash Royale API
2. Caches responses so repeated requests for the same data don't hit the
   real API again within a configurable time window
3. Enforces its own outgoing rate limit, so it never gets throttled or
   banned by the upstream API regardless of incoming traffic
4. Logs request metrics (cache hit rate, response times, request counts)

## Project status

Just started - scaffolding in progress.

## Roadmap

- [x] Project scaffold (ASP.NET Core Web API)
- [ ] Basic pass-through proxy to the real Clash Royale API
- [ ] In-memory caching layer
- [ ] Rate limiting on outgoing requests
- [ ] Request logging / metrics
- [ ] (Stretch) Redis-based caching instead of in-memory
- [ ] (Stretch) Simple stats dashboard endpoint

## Tech stack

- ASP.NET Core (C#)
- Official Clash Royale API (developer.clashroyale.com)

## Setup

1. Get a Clash Royale API key from developer.clashroyale.com (IP-restricted)
2. Add it to `appsettings.Development.json` (not committed to git):
```json
   {
     "ClashRoyale": {
       "ApiKey": "your_key_here"
     }
   }
```
3. Run the project from Visual Studio (green Run button), or:
```bash
   dotnet run
```

## Repository structure

```
ClashRoyaleApiGateway/
├── Controllers/     # API endpoints
├── Services/        # Business logic - caching, rate limiting, upstream calls
├── Middleware/       # Custom request pipeline components
├── Models/           # Data models / DTOs
├── Program.cs        # App entry point and configuration
└── appsettings.json
```