namespace Blindmesh.Api.Configuration;

/// <summary>API host configuration that is not part of the runtime services.</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>Exact origins that may send unsafe requests to the API.</summary>
    public string[] AllowedOrigins { get; init; } = ["http://localhost:3000", "http://127.0.0.1:3000"];

    /// <summary>
    /// Test-only authentication fixture. It may only be enabled outside
    /// production; see <see cref="Blindmesh.Application.Runtime.RuntimePolicy"/>.
    /// </summary>
    public bool TestAuthenticationEnabled { get; init; }
}
