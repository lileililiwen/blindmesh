namespace Blindmesh.Application.Identity;

/// <summary>
/// The configuration of the identity service. Every value is configuration;
/// no address, key or secret is compiled in.
/// </summary>
public sealed class IdentityOptions
{
    public const string SectionName = "Blindmesh:Identity";

    /// <summary>
    /// The browser visible address of the web application. The OAuth
    /// callback address is built from it, because the provider needs an
    /// absolute address.
    /// </summary>
    public string PublicBaseUrl { get; init; } = "http://localhost:3000";

    /// <summary>
    /// The path the browser uses to reach the API under the same origin. The
    /// proxy strips this prefix before the request reaches the API.
    /// </summary>
    public string BrowserApiPrefix { get; init; } = "/api/v1";

    /// <summary>
    /// The local GitHub fixture. It may only run with APP_ENV=test and it
    /// never runs in production.
    /// </summary>
    public bool LocalFixtureEnabled { get; init; }

    /// <summary>
    /// Secure uses the __Host- prefixed cookies. Development uses the
    /// explicitly non-__Host cookies that a plain HTTP host needs; it may
    /// never run in production.
    /// </summary>
    public string CookieSecurity { get; init; } = CookieSecurityModes.Secure;

    public GitHubOptions GitHub { get; init; } = new();

    /// <summary>The absolute address of the OAuth callback of the browser.</summary>
    public string CallbackUrl =>
        $"{PublicBaseUrl.TrimEnd('/')}/{BrowserApiPrefix.Trim('/')}/auth/github/callback";
}

public sealed class GitHubOptions
{
    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string AuthorizeUrl { get; init; } = "https://github.com/login/oauth/authorize";

    public string TokenUrl { get; init; } = "https://github.com/login/oauth/access_token";

    public string UserUrl { get; init; } = "https://api.github.com/user";

    public string UserEmailsUrl { get; init; } = "https://api.github.com/user/emails";

    /// <summary>The only scopes the product asks for.</summary>
    public string Scope { get; init; } = "read:user user:email";
}

public static class CookieSecurityModes
{
    public const string Secure = "Secure";

    public const string Development = "Development";
}
