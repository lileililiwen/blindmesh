namespace Blindmesh.Application.Identity;

/// <summary>
/// The cookies of the session. The production name carries the __Host- prefix,
/// which a browser only accepts over a secure connection. A plain HTTP host
/// uses the explicitly named development cookie; it may never run in
/// production.
/// </summary>
public sealed class IdentityCookiePolicy(IdentityOptions options)
{
    public const string SecureSessionCookieName = "__Host-blindmesh";

    public const string SecureOAuthCookieName = "__Host-blindmesh-oauth";

    public const string DevelopmentSessionCookieName = "blindmesh";

    public const string DevelopmentOAuthCookieName = "blindmesh-oauth";

    public bool IsSecure =>
        !string.Equals(options.CookieSecurity, CookieSecurityModes.Development, StringComparison.OrdinalIgnoreCase);

    public string SessionCookieName => IsSecure ? SecureSessionCookieName : DevelopmentSessionCookieName;

    public string OAuthCookieName => IsSecure ? SecureOAuthCookieName : DevelopmentOAuthCookieName;
}
