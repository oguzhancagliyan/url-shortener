using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api;
using UrlShortener.Application;
using UrlShortener.Application.Features.CreateShortUrl;
using UrlShortener.Application.Features.GetAnalytics;
using UrlShortener.Application.Features.ResolveShortUrl;
using UrlShortener.Infrastructure;
using UrlShortener.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("shortener-policy", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.SegmentsPerWindow = 4;
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseRateLimiter();

var api = app.MapGroup("/api")
    .RequireRateLimiting("shortener-policy")
    .WithTags("Shortener");

api.MapPost("/urls", async Task<Results<Created<CreateShortUrlResponse>, ValidationProblem>> (
    [FromBody] CreateShortUrlRequest request,
    HttpContext context,
    CreateShortUrlHandler handler,
    CancellationToken cancellationToken) =>
{
    if (!CreateShortUrlRequestValidator.TryValidate(request, out var errors))
    {
        return TypedResults.ValidationProblem(errors);
    }

    using var activity = new System.Diagnostics.ActivitySource(TelemetryConstants.ActivitySourceName)
        .StartActivity("create-short-url", System.Diagnostics.ActivityKind.Internal);

    var response = await handler.HandleAsync(
        new CreateShortUrlCommand(
            request.Url,
            request.ExpiresAtUtc,
            request.DeepLinks is null
                ? null
                : new DeepLinkTargetsCommand(
                    request.DeepLinks.IosUrl,
                    request.DeepLinks.AndroidUrl,
                    request.DeepLinks.DesktopUrl,
                    request.DeepLinks.FallbackUrl)),
        $"{context.Request.Scheme}://{context.Request.Host}",
        cancellationToken);

    return TypedResults.Created($"/api/urls/{response.Code}", response);
});

app.MapGet("/{code}", async Task<Results<RedirectHttpResult, NotFound>> (
    [FromRoute] string code,
    HttpContext context,
    ResolveShortUrlHandler handler,
    CancellationToken cancellationToken) =>
{
    var userAgent = context.Request.Headers.UserAgent.ToString();
    var target = await handler.HandleAsync(code, userAgent, cancellationToken);
    return target is null ? TypedResults.NotFound() : TypedResults.Redirect(target, permanent: false);
});

api.MapGet("/urls/{code}/analytics", async Task<Ok<AnalyticsResponse>> (
    [FromRoute] string code,
    GetAnalyticsHandler handler,
    CancellationToken cancellationToken) =>
{
    var analytics = await handler.HandleAsync(code, cancellationToken);
    return TypedResults.Ok(new AnalyticsResponse(analytics.Code, analytics.TotalResolutions, analytics.LastResolvedAtUtc));
});

app.Run();

public sealed record CreateShortUrlRequest(string Url, DateTimeOffset? ExpiresAtUtc, DeepLinkTargetsRequest? DeepLinks);

public sealed record DeepLinkTargetsRequest(
    string? IosUrl,
    string? AndroidUrl,
    string? DesktopUrl,
    string? FallbackUrl);

public sealed record AnalyticsResponse(string Code, long TotalResolutions, DateTimeOffset? LastResolvedAtUtc);

public partial class Program
{
}
