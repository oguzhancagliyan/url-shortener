namespace UrlShortener.Domain;

public sealed record DeepLinkTargets(
    string? IosUrl,
    string? AndroidUrl,
    string? DesktopUrl,
    string? FallbackUrl)
{
    public bool HasAny =>
        !string.IsNullOrWhiteSpace(IosUrl) ||
        !string.IsNullOrWhiteSpace(AndroidUrl) ||
        !string.IsNullOrWhiteSpace(DesktopUrl) ||
        !string.IsNullOrWhiteSpace(FallbackUrl);
}
