using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UrlShortener.Application.Abstractions;

namespace UrlShortener.IntegrationTests.Support;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IShortUrlRepository>();
            services.RemoveAll<IShortCodeGenerator>();
            services.AddSingleton<IShortUrlRepository, InMemoryShortUrlRepository>();
            services.AddSingleton<IShortCodeGenerator, IncrementingShortCodeGenerator>();
        });
    }
}
