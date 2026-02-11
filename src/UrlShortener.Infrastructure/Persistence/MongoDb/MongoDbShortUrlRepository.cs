using MongoDB.Driver;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;
using UrlShortener.Infrastructure.Persistence.Common;

namespace UrlShortener.Infrastructure.Persistence.MongoDb;

public sealed class MongoDbShortUrlRepository : BaseInstrumentedRepository, IShortUrlRepository
{
    private readonly IMongoCollection<ShortUrlDocument> _urls;
    private readonly IMongoCollection<ShortUrlAnalyticsDocument> _analytics;

    public MongoDbShortUrlRepository(IMongoClient client, string databaseName)
    {
        var db = client.GetDatabase(databaseName);
        _urls = db.GetCollection<ShortUrlDocument>("shorturls");
        _analytics = db.GetCollection<ShortUrlAnalyticsDocument>("shorturlanalytics");
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("code_exists", "mongodb");
        var count = await _urls.CountDocumentsAsync(x => x.Code == code, cancellationToken: cancellationToken);
        return count > 0;
    }

    public Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("insert_short_url", "mongodb");
        var document = new ShortUrlDocument
        {
            Code = shortUrl.Code,
            OriginalUrl = shortUrl.OriginalUrl,
            CreatedAtUtc = shortUrl.CreatedAtUtc.UtcDateTime,
            ExpiresAtUtc = shortUrl.ExpiresAtUtc?.UtcDateTime
        };

        return _urls.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_by_code", "mongodb");
        var document = await _urls.Find(x => x.Code == code).FirstOrDefaultAsync(cancellationToken);
        return document is null
            ? null
            : ShortUrl.Create(document.Code, document.OriginalUrl, document.ExpiresAtUtc is null ? null : DateTime.SpecifyKind(document.ExpiresAtUtc.Value, DateTimeKind.Utc));
    }

    public async Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("record_resolution", "mongodb");
        var update = Builders<ShortUrlAnalyticsDocument>.Update
            .Inc(x => x.TotalResolutions, 1)
            .Set(x => x.LastResolvedAtUtc, resolvedAtUtc.UtcDateTime);

        await _analytics.UpdateOneAsync(x => x.Code == code, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_analytics", "mongodb");
        var document = await _analytics.Find(x => x.Code == code).FirstOrDefaultAsync(cancellationToken);
        if (document is null) return new ShortUrlAnalytics { Code = code, TotalResolutions = 0 };

        return new ShortUrlAnalytics
        {
            Code = document.Code,
            TotalResolutions = document.TotalResolutions,
            LastResolvedAtUtc = document.LastResolvedAtUtc
        };
    }
}
