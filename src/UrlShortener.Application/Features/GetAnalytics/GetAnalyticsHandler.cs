using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;

namespace UrlShortener.Application.Features.GetAnalytics;

public sealed class GetAnalyticsHandler
{
    private readonly IShortUrlRepository _repository;

    public GetAnalyticsHandler(IShortUrlRepository repository)
    {
        _repository = repository;
    }

    public Task<ShortUrlAnalytics> HandleAsync(string code, CancellationToken cancellationToken) =>
        _repository.GetAnalyticsAsync(code, cancellationToken);
}
