namespace UrlShortener.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string DbProvider { get; set; } = "PostgreSQL";
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "urlshortener";
    public string BucketName { get; set; } = "urlshortener";
    public string ScopeName { get; set; } = "_default";
    public string CollectionName { get; set; } = "shorturls";
    public string CouchbaseUsername { get; set; } = "Administrator";
    public string CouchbasePassword { get; set; } = "password";
}
