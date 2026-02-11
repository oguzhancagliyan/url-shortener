using System.Threading;
using UrlShortener.Application.Abstractions;

namespace UrlShortener.IntegrationTests.Support;

public sealed class IncrementingShortCodeGenerator : IShortCodeGenerator
{
    private int _counter;

    public Task<string> GenerateUniqueCodeAsync(int length, CancellationToken cancellationToken)
    {
        var next = Interlocked.Increment(ref _counter);
        return Task.FromResult($"t{next:0000000}");
    }
}
