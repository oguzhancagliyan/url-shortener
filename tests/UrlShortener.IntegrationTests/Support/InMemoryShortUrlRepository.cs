using System.Collections.Concurrent;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;

namespace UrlShortener.IntegrationTests.Support;

public sealed class InMemoryShortUrlRepository : IShortUrlRepository
{
    private readonly ConcurrentDictionary<string, ShortUrl> _urls = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AnalyticsCounter> _analytics = new(StringComparer.Ordinal);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(_urls.ContainsKey(code));

    public Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
    {
        _urls[shortUrl.Code] = shortUrl;
        return Task.CompletedTask;
    }

    public Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        _urls.TryGetValue(code, out var value);
        return Task.FromResult(value);
    }

    public Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken)
    {
        _analytics.AddOrUpdate(
            code,
            _ => new AnalyticsCounter(1, resolvedAtUtc),
            (_, current) => current with
            {
                TotalResolutions = current.TotalResolutions + 1,
                LastResolvedAtUtc = resolvedAtUtc
            });
        return Task.CompletedTask;
    }

    public Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken)
    {
        if (!_analytics.TryGetValue(code, out var counter))
        {
            return Task.FromResult(new ShortUrlAnalytics { Code = code, TotalResolutions = 0L });
        }

        return Task.FromResult(new ShortUrlAnalytics
        {
            Code = code,
            TotalResolutions = counter.TotalResolutions,
            LastResolvedAtUtc = counter.LastResolvedAtUtc
        });
    }

    private sealed record AnalyticsCounter(long TotalResolutions, DateTimeOffset LastResolvedAtUtc);
}
