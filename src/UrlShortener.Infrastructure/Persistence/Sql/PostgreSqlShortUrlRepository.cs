using Npgsql;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;
using UrlShortener.Infrastructure.Persistence.Common;

namespace UrlShortener.Infrastructure.Persistence.Sql;

public sealed class PostgreSqlShortUrlRepository : BaseInstrumentedRepository, IShortUrlRepository
{
    private readonly string _connectionString;
    private bool? _supportsDeepLinks;

    public PostgreSqlShortUrlRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("code_exists", "postgresql");
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM short_urls WHERE code = @code";
        command.Parameters.AddWithValue("code", code);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return result > 0;
    }

    public async Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("insert_short_url", "postgresql");
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var supportsDeepLinks = await SupportsDeepLinksAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = supportsDeepLinks
            ? "INSERT INTO short_urls (id, code, original_url, created_at_utc, expires_at_utc, deep_link_ios, deep_link_android, deep_link_desktop, deep_link_fallback) VALUES (@id, @code, @originalUrl, @createdAtUtc, @expiresAtUtc, @deepLinkIos, @deepLinkAndroid, @deepLinkDesktop, @deepLinkFallback)"
            : "INSERT INTO short_urls (id, code, original_url, created_at_utc, expires_at_utc) VALUES (@id, @code, @originalUrl, @createdAtUtc, @expiresAtUtc)";
        command.Parameters.AddWithValue("id", shortUrl.Id);
        command.Parameters.AddWithValue("code", shortUrl.Code);
        command.Parameters.AddWithValue("originalUrl", shortUrl.OriginalUrl);
        command.Parameters.AddWithValue("createdAtUtc", shortUrl.CreatedAtUtc);
        command.Parameters.AddWithValue("expiresAtUtc", (object?)shortUrl.ExpiresAtUtc ?? DBNull.Value);
        if (supportsDeepLinks)
        {
            command.Parameters.AddWithValue("deepLinkIos", (object?)shortUrl.DeepLinks?.IosUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("deepLinkAndroid", (object?)shortUrl.DeepLinks?.AndroidUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("deepLinkDesktop", (object?)shortUrl.DeepLinks?.DesktopUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("deepLinkFallback", (object?)shortUrl.DeepLinks?.FallbackUrl ?? DBNull.Value);
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_by_code", "postgresql");
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var supportsDeepLinks = await SupportsDeepLinksAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = supportsDeepLinks
            ? "SELECT code, original_url, expires_at_utc, deep_link_ios, deep_link_android, deep_link_desktop, deep_link_fallback FROM short_urls WHERE code = @code LIMIT 1"
            : "SELECT code, original_url, expires_at_utc FROM short_urls WHERE code = @code LIMIT 1";
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var deepLinks = supportsDeepLinks
            ? CreateDeepLinks(
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6))
            : null;

        return ShortUrl.Create(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
            deepLinks);
    }

    public async Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("record_resolution", "postgresql");
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO short_url_analytics (code, total_resolutions, last_resolved_at_utc)
            VALUES (@code, 1, @resolvedAtUtc)
            ON CONFLICT (code)
            DO UPDATE SET
              total_resolutions = short_url_analytics.total_resolutions + 1,
              last_resolved_at_utc = EXCLUDED.last_resolved_at_utc;";
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("resolvedAtUtc", resolvedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_analytics", "postgresql");
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT code, total_resolutions, last_resolved_at_utc FROM short_url_analytics WHERE code = @code LIMIT 1";
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new ShortUrlAnalytics { Code = code, TotalResolutions = 0 };

        return new ShortUrlAnalytics
        {
            Code = reader.GetString(0),
            TotalResolutions = reader.GetInt64(1),
            LastResolvedAtUtc = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
        };
    }

    private static DeepLinkTargets? CreateDeepLinks(string? ios, string? android, string? desktop, string? fallback)
    {
        var deepLinks = new DeepLinkTargets(ios, android, desktop, fallback);
        return deepLinks.HasAny ? deepLinks : null;
    }

    private async Task<bool> SupportsDeepLinksAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        if (_supportsDeepLinks.HasValue)
        {
            return _supportsDeepLinks.Value;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(1)
            FROM information_schema.columns
            WHERE table_name = 'short_urls'
              AND table_schema = current_schema()
              AND column_name IN ('deep_link_ios', 'deep_link_android', 'deep_link_desktop', 'deep_link_fallback');";

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        _supportsDeepLinks = count == 4;
        return _supportsDeepLinks.Value;
    }
}
