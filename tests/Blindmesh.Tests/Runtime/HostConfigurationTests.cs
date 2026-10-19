using Blindmesh.Application.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Blindmesh.Tests.Runtime;

/// <summary>
/// The configuration of the host is the single source for every service. A
/// change that reads the configuration too early breaks the overrides of the
/// host, so the behaviour is covered.
/// </summary>
public sealed class HostConfigurationTests
{
    [Fact]
    public void FOUNDTheHostConfigurationOverridesReachEveryService()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Blindmesh:ObjectStorage:Endpoint"] = "http://127.0.0.1:9",
            ["Blindmesh:Mail:Host"] = "127.0.0.1",
            ["Blindmesh:Mail:Port"] = "9",
        };

        using var factory = new BlindmeshApiFactory(settings);

        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var options = factory.Services.GetRequiredService<IOptions<BlindmeshRuntimeOptions>>();

        Assert.Equal("http://127.0.0.1:9", configuration["Blindmesh:ObjectStorage:Endpoint"]);
        Assert.Equal("http://127.0.0.1:9", options.Value.ObjectStorage.Endpoint);
        Assert.Equal("9", options.Value.Mail.Port.ToString());
    }
}
