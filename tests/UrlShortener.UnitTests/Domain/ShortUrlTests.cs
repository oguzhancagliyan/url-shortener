using UrlShortener.Domain;

namespace UrlShortener.UnitTests.Domain;

public sealed class ShortUrlTests
{
    [Fact]
    public void ResolveTarget_WhenIosUserAgent_ReturnsIosDeepLink()
    {
        var sut = ShortUrl.Create(
            "abc12345",
            "https://example.com/web",
            null,
            new DeepLinkTargets(
                "myapp://ios/42",
                "myapp://android/42",
                "https://example.com/desktop",
                "https://example.com/fallback"));

        var resolved = sut.ResolveTarget("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)");

        Assert.Equal("myapp://ios/42", resolved);
    }

    [Fact]
    public void ResolveTarget_WhenAndroidUserAgent_ReturnsAndroidDeepLink()
    {
        var sut = ShortUrl.Create(
            "abc12345",
            "https://example.com/web",
            null,
            new DeepLinkTargets(
                "myapp://ios/42",
                "myapp://android/42",
                "https://example.com/desktop",
                "https://example.com/fallback"));

        var resolved = sut.ResolveTarget("Mozilla/5.0 (Linux; Android 14; Pixel 8)");

        Assert.Equal("myapp://android/42", resolved);
    }

    [Fact]
    public void ResolveTarget_WhenNoPlatformDeepLink_UsesFallbackThenOriginal()
    {
        var withFallback = ShortUrl.Create(
            "code-1",
            "https://example.com/original",
            null,
            new DeepLinkTargets(null, null, null, "https://example.com/fallback"));

        var withoutFallback = ShortUrl.Create(
            "code-2",
            "https://example.com/original",
            null,
            new DeepLinkTargets(null, null, null, null));

        Assert.Equal("https://example.com/fallback", withFallback.ResolveTarget("UnknownAgent"));
        Assert.Equal("https://example.com/original", withoutFallback.ResolveTarget("UnknownAgent"));
    }
}
