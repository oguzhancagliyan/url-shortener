using Couchbase.KeyValue;
using Couchbase.Query;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;
using UrlShortener.Infrastructure.Persistence.Common;

namespace UrlShortener.Infrastructure.Persistence.Couchbase;

public sealed class CouchbaseShortUrlRepository : BaseInstrumentedRepository, IShortUrlRepository
{
    private readonly IScope _scope;
    private readonly ICouchbaseCollection _collection;

    public CouchbaseShortUrlRepository(IScope scope, ICouchbaseCollection collection)
    {
        _scope = scope;
        _collection = collection;
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("code_exists", "couchbase");
        try
        {
            await _collection.GetAsync(code, options => options.CancellationToken(cancellationToken));
            return true;
        }
        catch (DocumentNotFoundException)
        {
            return false;
        }
    }

    public Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("insert_short_url", "couchbase");
        return _collection.InsertAsync(shortUrl.Code, shortUrl, options => options.CancellationToken(cancellationToken));
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_by_code", "couchbase");
        try
        {
            var result = await _collection.GetAsync(code, options => options.CancellationToken(cancellationToken));
            return result.ContentAs<ShortUrl>();
        }
        catch (DocumentNotFoundException)
        {
            return null;
        }
    }

    public async Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("record_resolution", "couchbase");
        const string statement = @"
            UPSERT INTO `shorturlanalytics` (KEY, VALUE)
            VALUES ($code, {""code"": $code, ""totalResolutions"": 1, ""lastResolvedAtUtc"": $resolvedAtUtc})
            RETURNING *;";

        await _scope.QueryAsync<dynamic>(statement, options =>
        {
            options.Parameter("code", code);
            options.Parameter("resolvedAtUtc", resolvedAtUtc.UtcDateTime);
            options.CancellationToken(cancellationToken);
        });
    }

    public async Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_analytics", "couchbase");
        const string statement = "SELECT a.code, a.totalResolutions, a.lastResolvedAtUtc FROM shorturlanalytics AS a USE KEYS $code";
        var result = await _scope.QueryAsync<ShortUrlAnalytics>(statement, options =>
        {
            options.Parameter("code", code);
            options.CancellationToken(cancellationToken);
        });

        await foreach (var row in result.Rows.ConfigureAwait(false))
        {
            return row;
        }

        return new ShortUrlAnalytics { Code = code, TotalResolutions = 0 };
    }
}
