using System.Security.Cryptography;
using UrlShortener.Application.Abstractions;

namespace UrlShortener.Application.Services;

public sealed class SecureShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private readonly IShortUrlRepository _repository;

    public SecureShortCodeGenerator(IShortUrlRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> GenerateUniqueCodeAsync(int length, CancellationToken cancellationToken)
    {
        const int maxAttempts = 8;
        for (var i = 0; i < maxAttempts; i++)
        {
            var candidate = Generate(length);
            if (!await _repository.CodeExistsAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique short code.");
    }

    private static string Generate(int length)
    {
        Span<byte> randomBytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[randomBytes[i] % Alphabet.Length];
        }

        return chars.ToString();
    }
}
