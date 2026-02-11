# URL Shortener (.NET 8, DDD + Vertical Slice)

Production-oriented URL shortener with runtime-switchable database providers, OpenTelemetry tracing, and security hardening.

## Solution structure

```text
UrlShortener.sln
src/
  UrlShortener.Api/              # Minimal API endpoints + runtime pipeline/security
  UrlShortener.Application/      # Vertical slices + use-case handlers
  UrlShortener.Domain/           # Domain entities and business invariants
  UrlShortener.Infrastructure/   # DB providers, DI wiring, OTEL setup
```

## Architecture highlights

- **DDD layering**
  - `Domain`: `ShortUrl`, `ShortUrlAnalytics`.
  - `Application`: use-case handlers (`Create`, `Resolve`, `GetAnalytics`) + abstractions.
  - `Infrastructure`: provider-specific repositories implementing a provider-agnostic interface.
  - `Api`: transport + input validation + rate-limiting + endpoint composition.
- **Vertical slices** in `Application/Features/*`.
- **Provider-agnostic repository** via `IShortUrlRepository`.

## Supported DB providers

Set via environment variable:

```bash
DbProvider=PostgreSQL
DbProvider=MongoDB
DbProvider=Couchbase
DbProvider=SqlServer
DbProvider=MySQL
```

Provider-specific connection strings are resolved using:

- `ConnectionStrings:PostgreSql`
- `ConnectionStrings:MongoDb`
- `ConnectionStrings:Couchbase`
- `ConnectionStrings:SqlServer`
- `ConnectionStrings:MySql`

## Security controls

- Sliding-window rate limiting (429 on rejection).
- Request input validation before handler dispatch.
- Secure random short-code generation using `RandomNumberGenerator`.
- Enumeration resistance on resolve endpoint via small randomized delay for misses.
- No sensitive information is returned in endpoint payloads.

## Observability

OpenTelemetry configured with:

- ASP.NET Core request tracing
- `HttpClient` tracing
- Custom application/database spans via `ActivitySource` (`UrlShortener.Application`)
- OTLP exporter (`OTEL_EXPORTER_OTLP_ENDPOINT`)

## API endpoints

- `POST /api/urls` → create short URL
- `GET /{code}` → resolve (redirect)
- `GET /api/urls/{code}/analytics` → analytics

## Local configuration

- `src/UrlShortener.Api/appsettings.json`
- `.env.example`

## Database DDL notes

Each provider repository expects:

- `ShortUrls` / `short_urls` table or equivalent collection
- `ShortUrlAnalytics` / `short_url_analytics` table or equivalent collection

You can manage schema with your preferred migration approach (e.g., FluentMigrator, DbUp, Flyway, Liquibase).
