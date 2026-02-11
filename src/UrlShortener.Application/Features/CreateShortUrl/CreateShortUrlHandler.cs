using UrlShortener.Application.Abstractions;
using UrlShortener.Domain;

namespace UrlShortener.Application.Features.CreateShortUrl;

public sealed class CreateShortUrlHandler
{
    private readonly IShortCodeGenerator _codeGenerator;
    private readonly IShortUrlRepository _repository;

    public CreateShortUrlHandler(IShortCodeGenerator codeGenerator, IShortUrlRepository repository)
    {
        _codeGenerator = codeGenerator;
        _repository = repository;
    }

    public async Task<CreateShortUrlResponse> HandleAsync(CreateShortUrlCommand command, string baseUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(command.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Invalid URL format.", nameof(command.Url));
        }

        var code = await _codeGenerator.GenerateUniqueCodeAsync(8, cancellationToken);
        var deepLinks = command.DeepLinks is { HasAny: true }
            ? new DeepLinkTargets(
                command.DeepLinks.IosUrl,
                command.DeepLinks.AndroidUrl,
                command.DeepLinks.DesktopUrl,
                command.DeepLinks.FallbackUrl ?? command.Url)
            : null;

        var entity = ShortUrl.Create(code, command.Url, command.ExpiresAtUtc, deepLinks);

        await _repository.CreateAsync(entity, cancellationToken);

        return new CreateShortUrlResponse(
            entity.Code,
            $"{baseUrl.TrimEnd('/')}/{entity.Code}",
            entity.CreatedAtUtc,
            entity.ExpiresAtUtc,
            entity.DeepLinks is null
                ? null
                : new DeepLinkTargetsResponse(
                    entity.DeepLinks.IosUrl,
                    entity.DeepLinks.AndroidUrl,
                    entity.DeepLinks.DesktopUrl,
                    entity.DeepLinks.FallbackUrl));
    }
}
