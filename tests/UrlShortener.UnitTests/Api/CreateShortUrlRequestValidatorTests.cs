using UrlShortener.Api;

namespace UrlShortener.UnitTests.Api;

public sealed class CreateShortUrlRequestValidatorTests
{
    [Fact]
    public void TryValidate_WhenDeepLinksContainJavaScriptUri_ReturnsError()
    {
        var request = new CreateShortUrlRequest(
            "https://example.com",
            null,
            new DeepLinkTargetsRequest("javascript:alert(1)", null, null, null));

        var isValid = CreateShortUrlRequestValidator.TryValidate(request, out var errors);

        Assert.False(isValid);
        Assert.Contains("DeepLinks.IosUrl", errors.Keys);
    }

    [Fact]
    public void TryValidate_WhenDeepLinksAreEmpty_ReturnsError()
    {
        var request = new CreateShortUrlRequest(
            "https://example.com",
            null,
            new DeepLinkTargetsRequest(null, null, null, null));

        var isValid = CreateShortUrlRequestValidator.TryValidate(request, out var errors);

        Assert.False(isValid);
        Assert.Contains("DeepLinks", errors.Keys);
    }
}
