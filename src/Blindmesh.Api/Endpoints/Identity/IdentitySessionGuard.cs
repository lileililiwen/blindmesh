using Blindmesh.Api.Infrastructure;
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
    IdentityCookiePolicy cookiePolicy,
    IAccountStandingReader standing)
{
    public const string CsrfHeaderName = "X-CSRF-Token";

    /// <summary>The raw session token of the request, if the caller sent one.</summary>
    public string? RawToken(HttpContext context) =>
        context.Request.Cookies[cookiePolicy.SessionCookieName];

    /// <summary>
    /// Resolves the session of the request. Null means no usable session. A
    /// banned account is refused here, before the actor of the request is
    /// handed out, so that no domain or monetary effect can happen.
    /// </summary>
    public async Task<SessionResolution?> ResolveAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var session = await identityService.ResolveSessionAsync(RawToken(context), cancellationToken);

        await RefuseBannedAccountAsync(session, cancellationToken);

        return session;
    }

    /// <summary>
    /// Refuses a banned account. The refusal happens before the actor of the
    /// request is handed out, so that no domain or monetary effect can happen.
    /// A request without a signed in account is never refused here: the ban
    /// belongs to an account, not to a visitor.
    /// </summary>
    public async Task RefuseBannedAccountAsync(
        SessionResolution? session,
        CancellationToken cancellationToken)
    {
        if (session is null || !session.IsAuthenticated)
        {
            return;
        }

        var account = await standing.GetAsync(session.UserId!.Value, cancellationToken);

        if (account.IsBanned)
        {
            throw new AccountBannedException();
        }
    }

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
