using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Identity;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Identity;

/// <summary>
/// The identity endpoints of the product. The API owns the GitHub
/// authorization code flow, the opaque session and the contributor profile.
/// The browser uses the same paths under the same origin prefix.
/// </summary>
public static class IdentityEndpoints
{
    public const string SignInErrorPath = "/sign-in";

    public static void MapIdentityEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/auth/github/start", HandleStartAsync)
            .WithName("StartGitHubSignIn")
            .WithSummary("Start a GitHub sign in and return the address of the provider.")
            .Produces<SignInStartView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/v1/auth/github/callback", HandleCallbackAsync)
            .WithName("CompleteGitHubSignIn")
            .WithSummary("Complete a GitHub sign in and open the session.")
            .Produces(StatusCodes.Status302Found)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/v1/session", HandleSessionAsync)
            .WithName("ReadSession")
            .WithSummary("Read the session of the caller and issue the CSRF token.")
            .Produces<SessionView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/logout", HandleLogoutAsync)
            .WithName("Logout")
            .WithSummary("Remove the server session of the caller.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        app.MapGet("/v1/profile", HandleGetProfileAsync)
            .WithName("ReadProfile")
            .WithSummary("Read the private profile of the signed in contributor.")
            .Produces<ProfileView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPatch("/v1/profile", HandleUpdateProfileAsync)
            .WithName("UpdateProfile")
            .WithSummary("Save the profile of the signed in contributor.")
            .Produces<ProfileView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/people/{username}", HandlePublicProfileAsync)
            .WithName("ReadPublicProfile")
            .WithSummary("Read the public profile of a contributor.")
            .Produces<PublicProfileView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleStartAsync(
        HttpContext context,
        IdentityService identityService,
        IdentitySessionGuard guard,
        IdentityCookiePolicy cookiePolicy,
        IOptions<IdentityOptions> identityOptions,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionInvalid,
                "The session of this account is no longer valid.");
        }

        if (session is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionRequired,
                "A session is required to start a sign in. Read the session first.");
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                IdentityErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session.");
        }

        if (!identityService.IsProviderConfigured)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status503ServiceUnavailable,
                IdentityErrorCodes.ProviderUnavailable,
                "The sign in provider is not available right now.");
        }

        var request = await IdentityRequestReader.ReadAsync<StartSignInRequest>(
            context.Request,
            IdentityRequestReader.SerializerOptions(jsonOptions),
            cancellationToken);

        if (request?.ReturnPath is not null && !ReturnPathPolicy.IsAllowed(request.ReturnPath))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                IdentityErrorCodes.Unprocessable,
                "The requested return path is not allowed.",
                new Dictionary<string, string[]>
                {
                    ["return_path"] = ["The sign in may only return to an allowlisted path of the product."],
                });
        }

        var start = await identityService.CreateOAuthStateAsync(request?.ReturnPath, cancellationToken);
        var actor = context.Request.Query["actor"].ToString();
        var authorizeUrl = identityService.BuildAuthorizeUrl(
            start.State,
            identityOptions.Value.CallbackUrl,
            string.IsNullOrWhiteSpace(actor) ? null : actor);

        context.Response.Cookies.Append(
            cookiePolicy.OAuthCookieName,
            start.Binding,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = cookiePolicy.IsSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true,
                Expires = start.ExpiresAt.UtcDateTime,
            });

        return Results.Ok(new SignInStartView(authorizeUrl, IdentityService.StateLifetimeMinutes * 60));
    }

    private static async Task<IResult> HandleCallbackAsync(
        HttpContext context,
        IdentityService identityService,
        IdentityCookiePolicy cookiePolicy,
        IOptions<IdentityOptions> identityOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var code = context.Request.Query["code"].ToString();
        var state = context.Request.Query["state"].ToString();

        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.Redirect($"{SignInErrorPath}?error=missing_code");
        }

        var binding = context.Request.Cookies[cookiePolicy.OAuthCookieName];
        var check = await identityService.CheckOAuthStateAsync(state, binding, cancellationToken);

        if (check.Status != OAuthStateStatus.Valid)
        {
            return Results.Redirect($"{SignInErrorPath}?error={ErrorSlug(check.Status)}");
        }

        ProviderIdentity identity;

        try
        {
            identity = await identityService.ExchangeAsync(
                code,
                identityOptions.Value.CallbackUrl,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The failure is written to the log of the host. The answer never
            // carries a provider detail and never carries the code.
            loggerFactory
                .CreateLogger("Blindmesh.Api.Endpoints.Identity")
                .LogWarning(
                    exception,
                    "The sign in provider could not be read for request {RequestId}.",
                    RequestIdMiddleware.GetRequestId(context));

            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status503ServiceUnavailable,
                IdentityErrorCodes.ProviderUnavailable,
                "The sign in provider could not be reached.");
        }

        var outcome = await identityService.CompleteSignInAsync(identity, check.StateId, cancellationToken);

        if (!outcome.Succeeded)
        {
            return Results.Redirect(
                $"{SignInErrorPath}?error={(outcome.StateReplayed ? "replayed_state" : "sign_in_failed")}");
        }

        var ticket = outcome.Ticket!;

        context.Response.Cookies.Append(
            cookiePolicy.SessionCookieName,
            ticket.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = cookiePolicy.IsSecure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true,
                Expires = ticket.ExpiresAt.UtcDateTime,
            });

        context.Response.Cookies.Delete(cookiePolicy.OAuthCookieName, new CookieOptions { Path = "/" });

        return Results.Redirect(check.ReturnPath);
    }

    private static async Task<IResult> HandleSessionAsync(
        HttpContext context,
        IdentityService identityService,
        IdentitySessionGuard guard,
        IdentityCookiePolicy cookiePolicy,
        CancellationToken cancellationToken)
    {
        var rawToken = guard.RawToken(context);
        var session = await identityService.ResolveSessionAsync(rawToken, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionInvalid,
                "The session of this account is no longer valid.");
        }

        if (session is null)
        {
            var ticket = await identityService.IssueSessionAsync(null, cancellationToken);

            context.Response.Cookies.Append(
                cookiePolicy.SessionCookieName,
                ticket.Token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = cookiePolicy.IsSecure,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    IsEssential = true,
                    Expires = ticket.ExpiresAt.UtcDateTime,
                });

            return Results.Ok(new SessionView(false, ticket.CsrfToken, ticket.ExpiresAt, null));
        }

        var user = session.UserId is null
            ? null
            : new SessionUserView(
                session.UserId.Value,
                session.Username,
                session.DisplayName,
                session.Role,
                session.GithubLogin);

        return Results.Ok(new SessionView(
            session.IsAuthenticated,
            SecretToken.DeriveCsrfToken(rawToken!),
            session.ExpiresAt,
            user));
    }

    private static async Task<IResult> HandleLogoutAsync(
        HttpContext context,
        IdentityService identityService,
        IdentitySessionGuard guard,
        IdentityCookiePolicy cookiePolicy,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionInvalid,
                "The session of this account is no longer valid.");
        }

        if (session is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionRequired,
                "There is no session to remove.");
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                IdentityErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session.");
        }

        await identityService.RevokeSessionAsync(session.SessionId, cancellationToken);

        context.Response.Cookies.Delete(cookiePolicy.SessionCookieName, new CookieOptions { Path = "/" });

        return Results.NoContent();
    }

    private static async Task<IResult> HandleGetProfileAsync(
        HttpContext context,
        IdentityService identityService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionInvalid,
                "The session of this account is no longer valid.");
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionRequired,
                "A signed in contributor is required to read the profile.");
        }

        var profile = await identityService.GetOwnProfileAsync(session.UserId!.Value, cancellationToken);

        if (profile is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status404NotFound,
                IdentityErrorCodes.NotFound,
                "The profile does not exist.");
        }

        return Results.Ok(profile);
    }

    private static async Task<IResult> HandleUpdateProfileAsync(
        HttpContext context,
        IdentityService identityService,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionInvalid,
                "The session of this account is no longer valid.");
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionRequired,
                "A signed in contributor is required to change the profile.");
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                IdentityErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session.");
        }

        var request = await IdentityRequestReader.ReadAsync<UpdateProfileRequest>(
            context.Request,
            IdentityRequestReader.SerializerOptions(jsonOptions),
            cancellationToken);

        if (request is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ApiErrorWriter.BadRequest,
                "The request body is not valid JSON.");
        }

        if (request.ExpectedVersion is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                IdentityErrorCodes.Unprocessable,
                "The expected version of the profile is required.",
                new Dictionary<string, string[]>
                {
                    ["expected_version"] = ["The version of the profile that was read is required."],
                });
        }

        if (request.UserId is not null && request.UserId != session.UserId)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                IdentityErrorCodes.Forbidden,
                "The profile of another account cannot be changed.");
        }

        var result = await identityService.UpdateProfileAsync(
            session.UserId!.Value,
            request.ExpectedVersion.Value,
            new ProfilePatch
            {
                Username = request.Username,
                DisplayName = request.DisplayName,
                Bio = request.Bio,
                Timezone = request.Timezone,
                Skills = request.Skills,
                Languages = request.Languages,
                IsPublic = request.IsPublic,
            },
            cancellationToken);

        return result.Status switch
        {
            UpdateProfileStatus.Updated => Results.Ok(result.Profile),
            UpdateProfileStatus.ValidationFailed => ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                IdentityErrorCodes.Unprocessable,
                "Some fields of the profile are not valid.",
                result.FieldErrors),
            UpdateProfileStatus.VersionConflict => ApiErrorWriter.Create(
                context,
                StatusCodes.Status409Conflict,
                IdentityErrorCodes.VersionConflict,
                $"The profile was changed elsewhere. The current version is {result.CurrentVersion}."),
            _ => ApiErrorWriter.Create(
                context,
                StatusCodes.Status404NotFound,
                IdentityErrorCodes.NotFound,
                "The profile does not exist."),
        };
    }

    private static async Task<IResult> HandlePublicProfileAsync(
        HttpContext context,
        IdentityService identityService,
        IdentitySessionGuard guard,
        string username,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                IdentityErrorCodes.SessionInvalid,
                "The session of this account is no longer valid.");
        }

        var profile = await identityService.GetPublicProfileAsync(username, cancellationToken);

        if (profile is null)
        {
            // A missing profile and a profile that is not public answer in the
            // same way, so the answer never reveals that an account exists.
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status404NotFound,
                IdentityErrorCodes.NotFound,
                "The profile does not exist.");
        }

        return Results.Ok(profile);
    }

    private static string ErrorSlug(OAuthStateStatus status) => status switch
    {
        OAuthStateStatus.Expired => "expired_state",
        OAuthStateStatus.Replayed => "replayed_state",
        OAuthStateStatus.Mismatched => "mismatched_state",
        _ => "invalid_state",
    };
}
