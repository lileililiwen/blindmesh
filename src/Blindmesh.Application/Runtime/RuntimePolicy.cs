namespace Blindmesh.Application.Runtime;

/// <summary>
/// Startup policy shared by the API host and its tests. The rules are decidable
/// and do not depend on the hosting environment.
/// </summary>
public static class RuntimePolicy
{
    public const string TestEnvironment = "test";
    public const string DevelopmentEnvironment = "development";
    public const string ProductionEnvironment = "production";

    /// <summary>
    /// Test authentication and other test-only fixtures may only run outside
    /// production. A production host with a test fixture enabled must fail before
    /// it accepts requests.
    /// </summary>
    public static void Validate(string appEnvironment, bool testAuthenticationEnabled)
    {
        if (!testAuthenticationEnabled)
        {
            return;
        }

        var environment = (appEnvironment ?? string.Empty).Trim().ToLowerInvariant();

        if (environment is TestEnvironment or DevelopmentEnvironment)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Test authentication is enabled while APP_ENV is '{appEnvironment}'. " +
            "It may only run with APP_ENV=test or APP_ENV=development.");
    }

    /// <summary>
    /// The local GitHub fixture serves fictional accounts, so it may only run
    /// with APP_ENV=test. A host that asks for it in any other environment
    /// fails before it accepts a request.
    /// </summary>
    public static void ValidateLocalOAuthFixture(string appEnvironment, bool fixtureEnabled)
    {
        if (!fixtureEnabled)
        {
            return;
        }

        var environment = (appEnvironment ?? string.Empty).Trim().ToLowerInvariant();

        if (environment == TestEnvironment)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The local OAuth fixture is enabled while APP_ENV is '{appEnvironment}'. " +
            "It may only run with APP_ENV=test.");
    }
}
