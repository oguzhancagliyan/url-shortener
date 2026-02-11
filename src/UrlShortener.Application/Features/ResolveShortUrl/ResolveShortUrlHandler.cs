using UrlShortener.Application.Abstractions;

namespace UrlShortener.Application.Features.ResolveShortUrl;

public sealed class ResolveShortUrlHandler
{
    private readonly IShortUrlRepository _repository;

    public ResolveShortUrlHandler(IShortUrlRepository repository)
    {
        _repository = repository;
    }

    public Task<string?> HandleAsync(string code, CancellationToken cancellationToken) =>
        HandleAsync(code, userAgent: null, cancellationToken);

    public async Task<string?> HandleAsync(string code, string? userAgent, CancellationToken cancellationToken)
    {
        var shortUrl = await _repository.GetByCodeAsync(code, cancellationToken);
        if (shortUrl is null || shortUrl.IsExpired(DateTimeOffset.UtcNow))
        {
            await Task.Delay(Random.Shared.Next(15, 45), cancellationToken);
            return null;
        }

        await _repository.RecordResolutionAsync(code, DateTimeOffset.UtcNow, cancellationToken);
        return shortUrl.ResolveTarget(userAgent);
    }
}
