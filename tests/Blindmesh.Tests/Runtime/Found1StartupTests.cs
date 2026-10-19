using System.Net;
using System.Text.Json;

namespace Blindmesh.Tests.Runtime;

/// <summary>
/// FOUND-1 Startup: the API starts, liveness and readiness report the real
/// state of the runtime, and a production host refuses a test fixture.
/// </summary>
[Collection(RuntimeCollection.Name)]
public sealed class Found1StartupTests
{
    private readonly RuntimeFixture _fixture;

    public Found1StartupTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    private BlindmeshApiFactory CreateFactory(
        IDictionary<string, string?>? overrides = null,
        string environment = "Test")
    {
        var settings = new Dictionary<string, string?>
        {
            ["Blindmesh:Database:ConnectionString"] = _fixture.ConnectionString,
            ["Blindmesh:ObjectStorage:Endpoint"] = _fixture.Dependencies.Endpoint,
            ["Blindmesh:ObjectStorage:HealthPath"] = "/minio/health/live",
            ["Blindmesh:Mail:Host"] = "127.0.0.1",
            ["Blindmesh:Mail:Port"] = _fixture.Dependencies.Port.ToString(),
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                settings[pair.Key] = pair.Value;
            }
        }

        return new BlindmeshApiFactory(settings, environment);
    }

    [Fact]
    public async Task LiveEndpointReportsTheProcessWithoutDependencyState()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/v1/health/live", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("alive", document.RootElement.GetProperty("status").GetString());
        Assert.True(document.RootElement.TryGetProperty("checked_at", out _));
        Assert.False(document.RootElement.TryGetProperty("dependencies", out _));
    }

    [Fact]
    public async Task ReadyEndpointReportsEveryDependencyWhenTheyAreReachable()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/v1/health/ready", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        Assert.Equal("available", document.RootElement.GetProperty("status").GetString());

        var dependencies = document.RootElement.GetProperty("dependencies")
            .EnumerateArray()
            .ToDictionary(
                dependency => dependency.GetProperty("name").GetString()!,
                dependency => dependency.GetProperty("status").GetString()!);

        Assert.Equal(3, dependencies.Count);
        Assert.True(dependencies["postgres"] == "up", body);
        Assert.True(dependencies["object-storage"] == "up", body);
        Assert.True(dependencies["mail"] == "up", body);
    }

    [Fact]
    public async Task ReadyEndpointReturnsServiceUnavailableWhenTheDatabaseIsUnreachable()
    {
        var settings = new Dictionary<string, string?>
        {
            // A port that nothing listens on. The database is the only dependency
            // that is taken away.
            ["Blindmesh:Database:ConnectionString"] =
                "Host=127.0.0.1;Port=1;Database=blindmesh;Username=blindmesh;Password=blindmesh;Timeout=2;Command Timeout=2",
        };

        using var factory = CreateFactory(settings);
        using var client = factory.CreateClient();

        using var ready = await client.GetAsync("/v1/health/ready", CancellationToken.None);
        var body = await ready.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.Equal("unavailable", document.RootElement.GetProperty("status").GetString());

        var dependencies = document.RootElement.GetProperty("dependencies")
            .EnumerateArray()
            .ToDictionary(
                dependency => dependency.GetProperty("name").GetString()!,
                dependency => dependency.GetProperty("status").GetString()!);

        Assert.Equal("down", dependencies["postgres"]);
        Assert.Equal("up", dependencies["object-storage"]);

        using var live = await client.GetAsync("/v1/health/live", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task ProductionStartupFailsWhenTestAuthenticationIsEnabled()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Api:TestAuthenticationEnabled"] = "true",
        };

        using var factory = CreateFactory(settings, environment: "Production");

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => Task.FromResult(factory.CreateClient()));

        Assert.Contains("Test authentication", Flatten(exception), StringComparison.OrdinalIgnoreCase);
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();

        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}
