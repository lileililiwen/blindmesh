using Blindmesh.Application.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Blindmesh.Infrastructure.Identity;

/// <summary>
/// The gate of the local OAuth fixture. The fixture may only run with
/// APP_ENV=test, so a production host can never serve a fictional identity.
/// The configuration is read when the gate is resolved, so the final
/// configuration of the host is the one that decides.
/// </summary>
public sealed class LocalGitHubFixtureGate
{
    public const string RequiredEnvironment = "test";

    public LocalGitHubFixtureGate(IConfiguration configuration, IHostEnvironment environment)
    {
        var requested = configuration
            .GetSection(IdentityOptions.SectionName)
            .GetValue<bool>(nameof(IdentityOptions.LocalFixtureEnabled));

        var appEnvironment = (configuration["APP_ENV"] ?? environment.EnvironmentName ?? string.Empty).Trim();

        IsEnabled = requested && string.Equals(appEnvironment, RequiredEnvironment, StringComparison.OrdinalIgnoreCase);

        AppEnvironment = appEnvironment;
        IsRequested = requested;
    }

    /// <summary>True when the fixture serves the seeded accounts.</summary>
    public bool IsEnabled { get; }

    /// <summary>True when the configuration asked for the fixture.</summary>
    public bool IsRequested { get; }

    /// <summary>The environment the host runs in.</summary>
    public string AppEnvironment { get; }
}
