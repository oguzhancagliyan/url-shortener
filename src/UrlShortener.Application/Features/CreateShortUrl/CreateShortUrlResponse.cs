namespace UrlShortener.Application.Features.CreateShortUrl;

public sealed record CreateShortUrlResponse(string Code, string ShortUrl, DateTimeOffset CreatedAtUtc, DateTimeOffset? ExpiresAtUtc);
