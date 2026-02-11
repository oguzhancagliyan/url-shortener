namespace UrlShortener.Application.Features.CreateShortUrl;

public sealed record CreateShortUrlResponse(
    string Code,
    string ShortUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DeepLinkTargetsResponse? DeepLinks);

public sealed record DeepLinkTargetsResponse(
    string? IosUrl,
    string? AndroidUrl,
    string? DesktopUrl,
    string? FallbackUrl);
