namespace Blindmesh.Application.Identity;

/// <summary>
/// The paths the OAuth callback is allowed to return to. Only a relative path
/// of this list is accepted, so a sign in can never be used to send a visitor
/// to another host.
/// </summary>
public static class ReturnPathPolicy
{
    public const string Default = "/onboarding";

    private static readonly string[] Allowed =
    [
        "/",
        "/onboarding",
        "/settings/profile",
    ];

    public static bool IsAllowed(string? path) =>
        path is not null && Array.Exists(Allowed, candidate => string.Equals(candidate, path, StringComparison.Ordinal));

    /// <summary>The allowlisted path, or the default when the path is not allowed.</summary>
    public static string Resolve(string? path) => IsAllowed(path) ? path! : Default;
}
