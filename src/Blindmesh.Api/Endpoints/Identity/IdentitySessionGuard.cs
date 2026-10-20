using Blindmesh.Application.Identity;

namespace Blindmesh.Api.Endpoints.Identity;

/// <summary>
/// The session of the current request. It resolves the session from the
/// cookie and checks the CSRF token of an unsafe request. The CSRF token is
/// bound to the server session, so a request without the matching token is
/// refused before it changes anything.
/// </summary>
public sealed class IdentitySessionGuard(
    IdentityService identityService,
    IdentityCookiePolicy cookiePolicy)
{
    public const string CsrfHeaderName = "X-CSRF-Token";

    /// <summary>The raw session token of the request, if the caller sent one.</summary>
    public string? RawToken(HttpContext context) =>
        context.Request.Cookies[cookiePolicy.SessionCookieName];

    /// <summary>Resolves the session of the request. Null means no usable session.</summary>
    public Task<SessionResolution?> ResolveAsync(HttpContext context, CancellationToken cancellationToken) =>
        identityService.ResolveSessionAsync(RawToken(context), cancellationToken);

    /// <summary>
    /// True when the request carries the CSRF token of its own session. A
    /// request without a session never has one, because the session is
    /// issued together with the token.
    /// </summary>
    public bool HasValidCsrfToken(HttpContext context)
    {
        var rawToken = RawToken(context);

        if (string.IsNullOrEmpty(rawToken))
        {
            return false;
        }

        var header = context.Request.Headers[CsrfHeaderName].ToString();

        return SecretToken.MatchesCsrfToken(rawToken, header);
    }
}
