using Npgsql;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;
using UrlShortener.Infrastructure.Persistence.Common;

namespace UrlShortener.Infrastructure.Persistence.Sql;

public sealed class PostgreSqlShortUrlRepository : BaseInstrumentedRepository, IShortUrlRepository
{
    private readonly string _connectionString;

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

        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO short_urls (id, code, original_url, created_at_utc, expires_at_utc) VALUES (@id, @code, @originalUrl, @createdAtUtc, @expiresAtUtc)";
        command.Parameters.AddWithValue("id", shortUrl.Id);
        command.Parameters.AddWithValue("code", shortUrl.Code);
        command.Parameters.AddWithValue("originalUrl", shortUrl.OriginalUrl);
        command.Parameters.AddWithValue("createdAtUtc", shortUrl.CreatedAtUtc);
        command.Parameters.AddWithValue("expiresAtUtc", (object?)shortUrl.ExpiresAtUtc ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("get_by_code", "postgresql");
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT code, original_url, expires_at_utc FROM short_urls WHERE code = @code LIMIT 1";
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ShortUrl.Create(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2));
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
}
