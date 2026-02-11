using Couchbase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using UrlShortener.Application.Abstractions;
using UrlShortener.Infrastructure.Configuration;
using UrlShortener.Infrastructure.Observability;
using UrlShortener.Infrastructure.Persistence.Couchbase;
using UrlShortener.Infrastructure.Persistence.MongoDb;
using UrlShortener.Infrastructure.Persistence.Sql;

namespace UrlShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.PostConfigure<DatabaseOptions>(options =>
        {
            var envProvider = configuration["DbProvider"];
            if (!string.IsNullOrWhiteSpace(envProvider))
            {
                options.DbProvider = envProvider;
            }

            options.ConnectionString = ResolveConnectionString(configuration, options.DbProvider, options.ConnectionString);
        });

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DatabaseOptions>>().Value);


        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<DatabaseOptions>();
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton<ICluster>(sp =>
        {
            var options = sp.GetRequiredService<DatabaseOptions>();
            if (!options.DbProvider.Equals("couchbase", StringComparison.OrdinalIgnoreCase))
            {
                return null!;
            }

            return Cluster.ConnectAsync(options.ConnectionString, options.CouchbaseUsername, options.CouchbasePassword).GetAwaiter().GetResult();
        });

        services.AddScoped<IShortUrlRepository>(sp =>
        {
            var options = sp.GetRequiredService<DatabaseOptions>();
            var provider = options.DbProvider.ToLowerInvariant();

            return provider switch
            {
                "postgresql" => new PostgreSqlShortUrlRepository(options.ConnectionString),
                "mysql" => new MySqlShortUrlRepository(options.ConnectionString),
                "mssql" or "sqlserver" => new SqlServerShortUrlRepository(options.ConnectionString),
                "mongodb" => BuildMongoRepository(sp, options),
                "couchbase" => BuildCouchbaseRepository(sp, options),
                _ => throw new InvalidOperationException($"Unsupported DbProvider: {options.DbProvider}")
            };
        });

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("UrlShortener.Api"))
            .WithTracing(tracing => tracing
                .AddSource(TelemetryConstants.ActivitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter());

        return services;
    }

    private static IShortUrlRepository BuildMongoRepository(IServiceProvider sp, DatabaseOptions options)
    {
        var client = sp.GetService<IMongoClient>() ?? new MongoClient(options.ConnectionString);
        return new MongoDbShortUrlRepository(client, options.DatabaseName);
    }

    private static IShortUrlRepository BuildCouchbaseRepository(IServiceProvider sp, DatabaseOptions options)
    {
        var cluster = sp.GetService<ICluster>();
        if (cluster is null)
        {
            throw new InvalidOperationException("Couchbase ICluster is not configured. Register Couchbase services and credentials.");
        }

        var bucket = cluster.BucketAsync(options.BucketName).GetAwaiter().GetResult();
        var scope = bucket.Scope(options.ScopeName);
        var collection = scope.Collection(options.CollectionName);

        return new CouchbaseShortUrlRepository(scope, collection);
    }

    private static string ResolveConnectionString(IConfiguration configuration, string provider, string fallback)
    {
        var providerKey = provider?.Trim().ToLowerInvariant();
        var preferred = providerKey switch
        {
            "postgresql" => configuration.GetConnectionString("PostgreSql"),
            "mongodb" => configuration.GetConnectionString("MongoDb"),
            "couchbase" => configuration.GetConnectionString("Couchbase"),
            "sqlserver" or "mssql" => configuration.GetConnectionString("SqlServer"),
            "mysql" => configuration.GetConnectionString("MySql"),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        return !string.IsNullOrWhiteSpace(fallback)
            ? fallback
            : throw new InvalidOperationException($"Connection string is required for provider '{provider}'.");
    }
}
