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
        var entity = ShortUrl.Create(code, command.Url, command.ExpiresAtUtc);

        await _repository.CreateAsync(entity, cancellationToken);

        return new CreateShortUrlResponse(
            entity.Code,
            $"{baseUrl.TrimEnd('/')}/{entity.Code}",
            entity.CreatedAtUtc,
            entity.ExpiresAtUtc);
    }
}
