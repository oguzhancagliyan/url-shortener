namespace UrlShortener.Application.Features.CreateShortUrl;

public sealed record CreateShortUrlCommand(string Url, DateTimeOffset? ExpiresAtUtc);
