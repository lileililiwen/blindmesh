using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Blindmesh.Tests.Runtime;

/// <summary>
/// Hosts the real API of the product. The configuration is applied when the
/// host is built, so the overrides reach every service of the application.
/// </summary>
public sealed class BlindmeshApiFactory : WebApplicationFactory<Program>
{
    private readonly IDictionary<string, string?> _settings;
    private readonly string _environment;

    public BlindmeshApiFactory(IDictionary<string, string?> settings, string environment = "Test")
    {
        _settings = settings;
        _environment = environment;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_settings));
    }
}
