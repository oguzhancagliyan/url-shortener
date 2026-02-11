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

### Deep link payload (optional)

`POST /api/urls` accepts optional deep links:

```json
{
  "url": "https://example.com/fallback",
  "expiresAtUtc": null,
  "deepLinks": {
    "iosUrl": "myapp://product/42",
    "androidUrl": "myapp://product/42",
    "desktopUrl": "https://example.com/product/42",
    "fallbackUrl": "https://example.com/fallback"
  }
}
```

Resolution behavior:

- iOS user-agent → `deepLinks.iosUrl` (if present)
- Android user-agent → `deepLinks.androidUrl` (if present)
- Other clients → `deepLinks.desktopUrl` (if present)
- Fallback chain → `deepLinks.fallbackUrl`, then `url`

## Local configuration

- `src/UrlShortener.Api/appsettings.json`
- `.env.example`

## Database DDL notes

Each provider repository expects:

- `ShortUrls` / `short_urls` table or equivalent collection
- `ShortUrlAnalytics` / `short_url_analytics` table or equivalent collection

If you want deep-link persistence in SQL providers, add these nullable columns to your URL table:

- SQL Server / MySQL: `DeepLinkIos`, `DeepLinkAndroid`, `DeepLinkDesktop`, `DeepLinkFallback`
- PostgreSQL: `deep_link_ios`, `deep_link_android`, `deep_link_desktop`, `deep_link_fallback`

You can manage schema with your preferred migration approach (e.g., FluentMigrator, DbUp, Flyway, Liquibase).
