using UrlShortener.Domain;

namespace UrlShortener.Application.Abstractions;

public interface IShortUrlRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
    Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken);
    Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken);
    Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken);
}
