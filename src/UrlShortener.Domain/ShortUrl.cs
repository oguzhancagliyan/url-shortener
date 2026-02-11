namespace UrlShortener.Domain;

public sealed class ShortUrl
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; }
    public string OriginalUrl { get; private set; }
    public DeepLinkTargets? DeepLinks { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    private ShortUrl(string code, string originalUrl, DateTimeOffset? expiresAtUtc, DeepLinkTargets? deepLinks)
    {
        Code = code;
        OriginalUrl = originalUrl;
        ExpiresAtUtc = expiresAtUtc;
        DeepLinks = deepLinks is { HasAny: true } ? deepLinks : null;
    }

    public static ShortUrl Create(string code, string originalUrl, DateTimeOffset? expiresAtUtc, DeepLinkTargets? deepLinks = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Short code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(originalUrl)) throw new ArgumentException("Original URL is required.", nameof(originalUrl));

        return new ShortUrl(code, originalUrl, expiresAtUtc, deepLinks);
    }

    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= nowUtc;

    public string ResolveTarget(string? userAgent)
    {
        if (DeepLinks is null)
        {
            return OriginalUrl;
        }

        var agent = userAgent ?? string.Empty;
        var isIos = agent.Contains("iphone", StringComparison.OrdinalIgnoreCase)
                    || agent.Contains("ipad", StringComparison.OrdinalIgnoreCase)
                    || agent.Contains("ipod", StringComparison.OrdinalIgnoreCase);
        var isAndroid = agent.Contains("android", StringComparison.OrdinalIgnoreCase);

        if (isIos && !string.IsNullOrWhiteSpace(DeepLinks.IosUrl))
        {
            return DeepLinks.IosUrl!;
        }

        if (isAndroid && !string.IsNullOrWhiteSpace(DeepLinks.AndroidUrl))
        {
            return DeepLinks.AndroidUrl!;
        }

        if (!isIos && !isAndroid && !string.IsNullOrWhiteSpace(DeepLinks.DesktopUrl))
        {
            return DeepLinks.DesktopUrl!;
        }

        if (!string.IsNullOrWhiteSpace(DeepLinks.FallbackUrl))
        {
            return DeepLinks.FallbackUrl!;
        }

        return OriginalUrl;
    }
}
