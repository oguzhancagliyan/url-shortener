using MySqlConnector;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;
using UrlShortener.Infrastructure.Persistence.Common;

namespace UrlShortener.Infrastructure.Persistence.Sql;

public sealed class MySqlShortUrlRepository : BaseInstrumentedRepository, IShortUrlRepository
{
    private readonly string _connectionString;
    private bool? _supportsDeepLinks;

    public MySqlShortUrlRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("code_exists", "mysql");
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM ShortUrls WHERE Code = @code";
        command.Parameters.AddWithValue("@code", code);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return result > 0;
    }

    public async Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("insert_short_url", "mysql");
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var supportsDeepLinks = await SupportsDeepLinksAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = supportsDeepLinks
            ? "INSERT INTO ShortUrls (Id, Code, OriginalUrl, CreatedAtUtc, ExpiresAtUtc, DeepLinkIos, DeepLinkAndroid, DeepLinkDesktop, DeepLinkFallback) VALUES (@id, @code, @originalUrl, @createdAtUtc, @expiresAtUtc, @deepLinkIos, @deepLinkAndroid, @deepLinkDesktop, @deepLinkFallback)"
            : "INSERT INTO ShortUrls (Id, Code, OriginalUrl, CreatedAtUtc, ExpiresAtUtc) VALUES (@id, @code, @originalUrl, @createdAtUtc, @expiresAtUtc)";
        command.Parameters.AddWithValue("@id", shortUrl.Id);
        command.Parameters.AddWithValue("@code", shortUrl.Code);
        command.Parameters.AddWithValue("@originalUrl", shortUrl.OriginalUrl);
        command.Parameters.AddWithValue("@createdAtUtc", shortUrl.CreatedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("@expiresAtUtc", shortUrl.ExpiresAtUtc?.UtcDateTime);
        if (supportsDeepLinks)
        {
            command.Parameters.AddWithValue("@deepLinkIos", shortUrl.DeepLinks?.IosUrl);
            command.Parameters.AddWithValue("@deepLinkAndroid", shortUrl.DeepLinks?.AndroidUrl);
            command.Parameters.AddWithValue("@deepLinkDesktop", shortUrl.DeepLinks?.DesktopUrl);
            command.Parameters.AddWithValue("@deepLinkFallback", shortUrl.DeepLinks?.FallbackUrl);
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_by_code", "mysql");
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var supportsDeepLinks = await SupportsDeepLinksAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = supportsDeepLinks
            ? "SELECT Code, OriginalUrl, ExpiresAtUtc, DeepLinkIos, DeepLinkAndroid, DeepLinkDesktop, DeepLinkFallback FROM ShortUrls WHERE Code = @code LIMIT 1"
            : "SELECT Code, OriginalUrl, ExpiresAtUtc FROM ShortUrls WHERE Code = @code LIMIT 1";
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var deepLinks = supportsDeepLinks
            ? CreateDeepLinks(
                reader.IsDBNull(reader.GetOrdinal("DeepLinkIos")) ? null : reader.GetString("DeepLinkIos"),
                reader.IsDBNull(reader.GetOrdinal("DeepLinkAndroid")) ? null : reader.GetString("DeepLinkAndroid"),
                reader.IsDBNull(reader.GetOrdinal("DeepLinkDesktop")) ? null : reader.GetString("DeepLinkDesktop"),
                reader.IsDBNull(reader.GetOrdinal("DeepLinkFallback")) ? null : reader.GetString("DeepLinkFallback"))
            : null;

        return ShortUrl.Create(
            reader.GetString("Code"),
            reader.GetString("OriginalUrl"),
            reader.IsDBNull(reader.GetOrdinal("ExpiresAtUtc")) ? null : new DateTimeOffset(reader.GetDateTime("ExpiresAtUtc"), TimeSpan.Zero),
            deepLinks);
    }

    public async Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("record_resolution", "mysql");
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ShortUrlAnalytics (Code, TotalResolutions, LastResolvedAtUtc)
            VALUES (@code, 1, @resolvedAtUtc)
            ON DUPLICATE KEY UPDATE
            TotalResolutions = TotalResolutions + 1,
            LastResolvedAtUtc = VALUES(LastResolvedAtUtc);";
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@resolvedAtUtc", resolvedAtUtc.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_analytics", "mysql");
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Code, TotalResolutions, LastResolvedAtUtc FROM ShortUrlAnalytics WHERE Code = @code LIMIT 1";
        command.Parameters.AddWithValue("@code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new ShortUrlAnalytics { Code = code, TotalResolutions = 0 };

        return new ShortUrlAnalytics
        {
            Code = reader.GetString("Code"),
            TotalResolutions = reader.GetInt64("TotalResolutions"),
            LastResolvedAtUtc = reader.IsDBNull(reader.GetOrdinal("LastResolvedAtUtc")) ? null : new DateTimeOffset(reader.GetDateTime("LastResolvedAtUtc"), TimeSpan.Zero)
        };
    }

    private static DeepLinkTargets? CreateDeepLinks(string? ios, string? android, string? desktop, string? fallback)
    {
        var deepLinks = new DeepLinkTargets(ios, android, desktop, fallback);
        return deepLinks.HasAny ? deepLinks : null;
    }

    private async Task<bool> SupportsDeepLinksAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (_supportsDeepLinks.HasValue)
        {
            return _supportsDeepLinks.Value;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = 'ShortUrls'
              AND column_name IN ('DeepLinkIos', 'DeepLinkAndroid', 'DeepLinkDesktop', 'DeepLinkFallback');";

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        _supportsDeepLinks = count == 4;
        return _supportsDeepLinks.Value;
    }
}
