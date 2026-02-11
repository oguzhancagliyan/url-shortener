using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests;

public sealed class ApiEndpointsTests : IClassFixture<TestApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public ApiEndpointsTests(TestApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task PostUrls_WithDeepLinks_ReturnsCreatedResponse()
    {
        var request = new
        {
            url = "https://example.com/fallback",
            expiresAtUtc = (DateTimeOffset?)null,
            deepLinks = new
            {
                iosUrl = "myapp://product/42",
                androidUrl = "myapp://product/42",
                desktopUrl = "https://example.com/product/42",
                fallbackUrl = "https://example.com/fallback"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/urls", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateShortUrlApiResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Code));
        Assert.NotNull(payload.DeepLinks);
        Assert.Equal("myapp://product/42", payload.DeepLinks!.IosUrl);
    }

    [Fact]
    public async Task GetCode_WithIosUserAgent_RedirectsToIosDeepLink()
    {
        var create = await _client.PostAsJsonAsync("/api/urls", new
        {
            url = "https://example.com/fallback",
            expiresAtUtc = (DateTimeOffset?)null,
            deepLinks = new
            {
                iosUrl = "myapp://ios/42",
                androidUrl = "myapp://android/42",
                desktopUrl = "https://example.com/desktop/42",
                fallbackUrl = "https://example.com/fallback"
            }
        });
        create.EnsureSuccessStatusCode();
        var createdPayload = await create.Content.ReadFromJsonAsync<CreateShortUrlApiResponse>(JsonOptions);
        Assert.NotNull(createdPayload);

        using var resolveRequest = new HttpRequestMessage(HttpMethod.Get, $"/{createdPayload!.Code}");
        resolveRequest.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)");
        var resolve = await _client.SendAsync(resolveRequest);

        Assert.Equal(HttpStatusCode.Redirect, resolve.StatusCode);
        Assert.NotNull(resolve.Headers.Location);
        Assert.Equal("myapp://ios/42", resolve.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Analytics_AfterTwoResolutions_ReturnsCount()
    {
        var create = await _client.PostAsJsonAsync("/api/urls", new
        {
            url = "https://example.com/main",
            expiresAtUtc = (DateTimeOffset?)null,
            deepLinks = new
            {
                androidUrl = "myapp://android/42"
            }
        });
        create.EnsureSuccessStatusCode();
        var createdPayload = await create.Content.ReadFromJsonAsync<CreateShortUrlApiResponse>(JsonOptions);
        Assert.NotNull(createdPayload);
        var code = createdPayload!.Code;

        using var firstResolve = new HttpRequestMessage(HttpMethod.Get, $"/{code}");
        firstResolve.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Linux; Android 14)");
        var firstResponse = await _client.SendAsync(firstResolve);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        using var secondResolve = new HttpRequestMessage(HttpMethod.Get, $"/{code}");
        secondResolve.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Linux; Android 14)");
        var secondResponse = await _client.SendAsync(secondResolve);
        Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);

        var analytics = await _client.GetFromJsonAsync<AnalyticsApiResponse>($"/api/urls/{code}/analytics", JsonOptions);

        Assert.NotNull(analytics);
        Assert.Equal(code, analytics!.Code);
        Assert.Equal(2L, analytics.TotalResolutions);
    }

    public sealed record CreateShortUrlApiResponse(
        string Code,
        string ShortUrl,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ExpiresAtUtc,
        DeepLinksApiResponse? DeepLinks);

    public sealed record DeepLinksApiResponse(
        string? IosUrl,
        string? AndroidUrl,
        string? DesktopUrl,
        string? FallbackUrl);

    public sealed record AnalyticsApiResponse(string Code, long TotalResolutions, DateTimeOffset? LastResolvedAtUtc);
}
