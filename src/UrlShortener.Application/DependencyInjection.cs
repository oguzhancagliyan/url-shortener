using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Features.CreateShortUrl;
using UrlShortener.Application.Features.GetAnalytics;
using UrlShortener.Application.Features.ResolveShortUrl;
using UrlShortener.Application.Services;

namespace UrlShortener.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateShortUrlHandler>();
        services.AddScoped<ResolveShortUrlHandler>();
        services.AddScoped<GetAnalyticsHandler>();
        services.AddScoped<IShortCodeGenerator, SecureShortCodeGenerator>();
        return services;
    }
}
