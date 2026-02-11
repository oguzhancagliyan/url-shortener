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

        return errors.Count == 0;
    }
}
