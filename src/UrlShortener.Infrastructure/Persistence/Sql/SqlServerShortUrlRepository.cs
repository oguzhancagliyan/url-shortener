using Microsoft.Data.SqlClient;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;
using UrlShortener.Infrastructure.Persistence.Common;

namespace UrlShortener.Infrastructure.Persistence.Sql;

public sealed class SqlServerShortUrlRepository : BaseInstrumentedRepository, IShortUrlRepository
{
    private readonly string _connectionString;
    private bool? _supportsDeepLinks;

    public SqlServerShortUrlRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("code_exists", "mssql");
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM ShortUrls WHERE Code = @Code";
        command.Parameters.AddWithValue("@Code", code);

        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return result > 0;
    }

    public async Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("insert_short_url", "mssql");
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var supportsDeepLinks = await SupportsDeepLinksAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = supportsDeepLinks
            ? @"INSERT INTO ShortUrls (Id, Code, OriginalUrl, CreatedAtUtc, ExpiresAtUtc, DeepLinkIos, DeepLinkAndroid, DeepLinkDesktop, DeepLinkFallback) VALUES (@Id, @Code, @OriginalUrl, @CreatedAtUtc, @ExpiresAtUtc, @DeepLinkIos, @DeepLinkAndroid, @DeepLinkDesktop, @DeepLinkFallback)"
            : @"INSERT INTO ShortUrls (Id, Code, OriginalUrl, CreatedAtUtc, ExpiresAtUtc) VALUES (@Id, @Code, @OriginalUrl, @CreatedAtUtc, @ExpiresAtUtc)";
        command.Parameters.AddWithValue("@Id", shortUrl.Id);
        command.Parameters.AddWithValue("@Code", shortUrl.Code);
        command.Parameters.AddWithValue("@OriginalUrl", shortUrl.OriginalUrl);
        command.Parameters.AddWithValue("@CreatedAtUtc", shortUrl.CreatedAtUtc);
        command.Parameters.AddWithValue("@ExpiresAtUtc", shortUrl.ExpiresAtUtc ?? (object)DBNull.Value);
        if (supportsDeepLinks)
        {
            command.Parameters.AddWithValue("@DeepLinkIos", (object?)shortUrl.DeepLinks?.IosUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@DeepLinkAndroid", (object?)shortUrl.DeepLinks?.AndroidUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@DeepLinkDesktop", (object?)shortUrl.DeepLinks?.DesktopUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@DeepLinkFallback", (object?)shortUrl.DeepLinks?.FallbackUrl ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_by_code", "mssql");
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var supportsDeepLinks = await SupportsDeepLinksAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = supportsDeepLinks
            ? "SELECT TOP 1 Code, OriginalUrl, ExpiresAtUtc, DeepLinkIos, DeepLinkAndroid, DeepLinkDesktop, DeepLinkFallback FROM ShortUrls WHERE Code = @Code"
            : "SELECT TOP 1 Code, OriginalUrl, ExpiresAtUtc FROM ShortUrls WHERE Code = @Code";
        command.Parameters.AddWithValue("@Code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var deepLinks = supportsDeepLinks
            ? CreateDeepLinks(
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6))
            : null;

        return ShortUrl.Create(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetDateTimeOffset(2), deepLinks);
    }

    public async Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("record_resolution", "mssql");
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            MERGE ShortUrlAnalytics AS target
            USING (SELECT @Code AS Code) AS source
            ON target.Code = source.Code
            WHEN MATCHED THEN UPDATE SET TotalResolutions = target.TotalResolutions + 1, LastResolvedAtUtc = @ResolvedAtUtc
            WHEN NOT MATCHED THEN INSERT (Code, TotalResolutions, LastResolvedAtUtc) VALUES (@Code, 1, @ResolvedAtUtc);";
        command.Parameters.AddWithValue("@Code", code);
        command.Parameters.AddWithValue("@ResolvedAtUtc", resolvedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_analytics", "mssql");
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP 1 Code, TotalResolutions, LastResolvedAtUtc FROM ShortUrlAnalytics WHERE Code = @Code";
        command.Parameters.AddWithValue("@Code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ShortUrlAnalytics { Code = code, TotalResolutions = 0 };
        }

        return new ShortUrlAnalytics
        {
            Code = reader.GetString(0),
            TotalResolutions = reader.GetInt64(1),
            LastResolvedAtUtc = reader.IsDBNull(2) ? null : reader.GetDateTimeOffset(2)
        };
    }

    private static DeepLinkTargets? CreateDeepLinks(string? ios, string? android, string? desktop, string? fallback)
    {
        var deepLinks = new DeepLinkTargets(ios, android, desktop, fallback);
        return deepLinks.HasAny ? deepLinks : null;
    }

    private async Task<bool> SupportsDeepLinksAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        if (_supportsDeepLinks.HasValue)
        {
            return _supportsDeepLinks.Value;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(1)
            FROM sys.columns
            WHERE object_id = OBJECT_ID('ShortUrls')
              AND name IN ('DeepLinkIos', 'DeepLinkAndroid', 'DeepLinkDesktop', 'DeepLinkFallback');";

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        _supportsDeepLinks = count == 4;
        return _supportsDeepLinks.Value;
    }
}
