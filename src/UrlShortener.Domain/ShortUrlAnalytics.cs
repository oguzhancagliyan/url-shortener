namespace UrlShortener.Domain;

public sealed class ShortUrlAnalytics
{
    public string Code { get; init; } = string.Empty;
    public long TotalResolutions { get; init; }
    public DateTimeOffset? LastResolvedAtUtc { get; init; }
}
