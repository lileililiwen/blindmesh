using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Tests.Runtime;

/// <summary>
/// Hosts the real API of the product. The configuration is applied when the
/// host is built, so the overrides reach every service of the application.
/// The services of the host may be replaced as well, so a test can run against
/// a stand in of an external service.
/// </summary>
public sealed class BlindmeshApiFactory : WebApplicationFactory<Program>
{
    private readonly IDictionary<string, string?> _settings;
    private readonly string _environment;
    private readonly Action<IServiceCollection>? _configureServices;

    public BlindmeshApiFactory(
        IDictionary<string, string?> settings,
        string environment = "Test",
        Action<IServiceCollection>? configureServices = null)
    {
        _settings = settings;
        _environment = environment;
        _configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_settings));

        if (_configureServices is not null)
        {
            builder.ConfigureTestServices(_configureServices);
        }
    }
}
