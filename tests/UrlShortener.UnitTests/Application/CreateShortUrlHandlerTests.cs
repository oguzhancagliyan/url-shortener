using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Features.CreateShortUrl;
using UrlShortener.Domain;

namespace UrlShortener.UnitTests.Application;

public sealed class CreateShortUrlHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDeepLinksProvided_PersistsAndReturnsThem()
    {
        var repository = new CapturingShortUrlRepository();
        var handler = new CreateShortUrlHandler(new FixedCodeGenerator("test1234"), repository);

        var response = await handler.HandleAsync(
            new CreateShortUrlCommand(
                "https://example.com/product/42",
                null,
                new DeepLinkTargetsCommand(
                    "myapp://product/42",
                    "myapp://product/42",
                    "https://example.com/product/42",
                    null)),
            "https://sho.rt",
            CancellationToken.None);

        Assert.Equal("test1234", response.Code);
        Assert.Equal("https://sho.rt/test1234", response.ShortUrl);
        Assert.NotNull(response.DeepLinks);
        Assert.Equal("myapp://product/42", response.DeepLinks!.IosUrl);
        Assert.Equal("https://example.com/product/42", response.DeepLinks.FallbackUrl);
        Assert.NotNull(repository.Created);
        Assert.NotNull(repository.Created!.DeepLinks);
        Assert.Equal("https://example.com/product/42", repository.Created.DeepLinks!.FallbackUrl);
    }

    [Fact]
    public async Task HandleAsync_WhenUrlIsInvalid_ThrowsArgumentException()
    {
        var repository = new CapturingShortUrlRepository();
        var handler = new CreateShortUrlHandler(new FixedCodeGenerator("test1234"), repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(
                new CreateShortUrlCommand("javascript:alert(1)", null, null),
                "https://sho.rt",
                CancellationToken.None));
    }

    private sealed class FixedCodeGenerator : IShortCodeGenerator
    {
        private readonly string _code;

        public FixedCodeGenerator(string code)
        {
            _code = code;
        }

        public Task<string> GenerateUniqueCodeAsync(int length, CancellationToken cancellationToken) =>
            Task.FromResult(_code);
    }

    private sealed class CapturingShortUrlRepository : IShortUrlRepository
    {
        public ShortUrl? Created { get; private set; }

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
        {
            Created = shortUrl;
            return Task.CompletedTask;
        }

        public Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult<ShortUrl?>(null);

        public Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(new ShortUrlAnalytics { Code = code, TotalResolutions = 0 });
    }
}
