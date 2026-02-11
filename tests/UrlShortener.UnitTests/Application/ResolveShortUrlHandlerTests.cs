using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Features.ResolveShortUrl;
using UrlShortener.Domain;

namespace UrlShortener.UnitTests.Application;

public sealed class ResolveShortUrlHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenEntityExists_RecordsResolutionAndReturnsTarget()
    {
        var shortUrl = ShortUrl.Create(
            "abc12345",
            "https://example.com/original",
            null,
            new DeepLinkTargets("myapp://ios/42", null, null, null));
        var repository = new StubShortUrlRepository(shortUrl);
        var handler = new ResolveShortUrlHandler(repository);

        var result = await handler.HandleAsync("abc12345", "iPhone", CancellationToken.None);

        Assert.Equal("myapp://ios/42", result);
        Assert.Equal(1, repository.RecordResolutionCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenEntityMissing_ReturnsNullWithoutRecording()
    {
        var repository = new StubShortUrlRepository(null);
        var handler = new ResolveShortUrlHandler(repository);

        var result = await handler.HandleAsync("missing", "iPhone", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, repository.RecordResolutionCalls);
    }

    private sealed class StubShortUrlRepository : IShortUrlRepository
    {
        private readonly ShortUrl? _entity;

        public StubShortUrlRepository(ShortUrl? entity)
        {
            _entity = entity;
        }

        public int RecordResolutionCalls { get; private set; }

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task CreateAsync(ShortUrl shortUrl, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(_entity);

        public Task RecordResolutionAsync(string code, DateTimeOffset resolvedAtUtc, CancellationToken cancellationToken)
        {
            RecordResolutionCalls++;
            return Task.CompletedTask;
        }

        public Task<ShortUrlAnalytics> GetAnalyticsAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(new ShortUrlAnalytics { Code = code, TotalResolutions = 0 });
    }
}
