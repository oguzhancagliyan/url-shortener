namespace UrlShortener.Api;

public static class CreateShortUrlRequestValidator
{
    public static bool TryValidate(CreateShortUrlRequest request, out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            errors[nameof(request.Url)] = ["URL is required."];
        }
        else if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors[nameof(request.Url)] = ["URL must be a valid HTTP/HTTPS absolute URL."];
        }

        if (request.ExpiresAtUtc is not null && request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            errors[nameof(request.ExpiresAtUtc)] = ["Expiration date must be in the future."];
        }

        if (request.DeepLinks is not null)
        {
            ValidateDeepLinks(request.DeepLinks, errors);
        }

        return errors.Count == 0;
    }

    private static void ValidateDeepLinks(DeepLinkTargetsRequest deepLinks, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(deepLinks.IosUrl)
            && string.IsNullOrWhiteSpace(deepLinks.AndroidUrl)
            && string.IsNullOrWhiteSpace(deepLinks.DesktopUrl)
            && string.IsNullOrWhiteSpace(deepLinks.FallbackUrl))
        {
            errors[nameof(CreateShortUrlRequest.DeepLinks)] = ["At least one deep link target must be provided when deepLinks is set."];
            return;
        }

        ValidateAbsoluteUri(deepLinks.IosUrl, $"{nameof(CreateShortUrlRequest.DeepLinks)}.{nameof(DeepLinkTargetsRequest.IosUrl)}", false, errors);
        ValidateAbsoluteUri(deepLinks.AndroidUrl, $"{nameof(CreateShortUrlRequest.DeepLinks)}.{nameof(DeepLinkTargetsRequest.AndroidUrl)}", false, errors);
        ValidateAbsoluteUri(deepLinks.DesktopUrl, $"{nameof(CreateShortUrlRequest.DeepLinks)}.{nameof(DeepLinkTargetsRequest.DesktopUrl)}", true, errors);
        ValidateAbsoluteUri(deepLinks.FallbackUrl, $"{nameof(CreateShortUrlRequest.DeepLinks)}.{nameof(DeepLinkTargetsRequest.FallbackUrl)}", true, errors);
    }

    private static void ValidateAbsoluteUri(string? url, string key, bool requireWebScheme, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            errors[key] = ["Value must be a valid absolute URL."];
            return;
        }

        if (string.Equals(uri.Scheme, "javascript", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
        {
            errors[key] = ["URL scheme is not allowed."];
            return;
        }

        if (requireWebScheme
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            errors[key] = ["URL must use HTTP or HTTPS."];
        }
    }
}
