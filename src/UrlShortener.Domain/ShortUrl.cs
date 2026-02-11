namespace UrlShortener.Domain;

public sealed class ShortUrl
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; }
    public string OriginalUrl { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    private ShortUrl(string code, string originalUrl, DateTimeOffset? expiresAtUtc)
    {
        Code = code;
        OriginalUrl = originalUrl;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static ShortUrl Create(string code, string originalUrl, DateTimeOffset? expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Short code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(originalUrl)) throw new ArgumentException("Original URL is required.", nameof(originalUrl));

        return new ShortUrl(code, originalUrl, expiresAtUtc);
    }

    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= nowUtc;
}
