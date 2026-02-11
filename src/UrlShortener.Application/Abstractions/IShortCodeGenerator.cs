namespace UrlShortener.Application.Abstractions;

public interface IShortCodeGenerator
{
    Task<string> GenerateUniqueCodeAsync(int length, CancellationToken cancellationToken);
}
