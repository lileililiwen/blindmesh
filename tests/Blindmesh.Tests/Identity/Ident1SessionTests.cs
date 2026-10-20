using System.Net;
using System.Text.Json;
using Blindmesh.Application.Identity;
using Blindmesh.Domain.Identity;
using Blindmesh.Infrastructure.Identity;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Identity;

/// <summary>
/// IDENT-1 Session: the GitHub callback issues an opaque session, the state is
/// used once, an expired or mismatched state fails safely and a banned
/// account loses its access on the next request.
/// </summary>
[Collection(RuntimeCollection.Name)]
public sealed class Ident1SessionTests
{
    private readonly RuntimeFixture _fixture;

    public Ident1SessionTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IDENT1SuccessTheCallbackOpensTheSessionAndStoresNoProviderToken()
    {
        using var host = new IdentityTestHost(_fixture);

        var sessionsBefore = await host.CountSessionsAsync();

        var callback = await host.SignInAsync("alice");

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal("/onboarding", callback.Headers.Location?.OriginalString);

        var cookie = callback.Headers.GetValues("Set-Cookie")
            .First(value => value.StartsWith($"{IdentityTestHost.SessionCookieName}=", StringComparison.Ordinal));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);

        // The sign in created exactly one session.
        Assert.Equal(sessionsBefore + 1, await host.CountSessionsAsync());

        // The session of the signed in contributor only holds the hash of the
        // opaque token.
        var session = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Sessions
                .Where(item => item.UserId != null)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync());

        Assert.NotNull(session);
        Assert.Equal(64, session!.TokenHash.Length);
        Assert.DoesNotContain("blindmesh", session.TokenHash, StringComparison.OrdinalIgnoreCase);

        var user = await host.FindUserBySubjectAsync("1001");

        Assert.NotNull(user);
        Assert.StartsWith("alice", user!.Username, StringComparison.Ordinal);
        Assert.Equal(UserRole.User, user.Role);
        Assert.False(user.IsBanned);
        Assert.Equal("alice@blindmesh.test", user.Email);

        // The access token of the provider is never stored.
        var leaked = await host.WithDatabaseAsync(async dbContext =>
        {
            var users = await dbContext.Users
                .Where(item => item.GithubLogin.Contains(LocalGitHubFixture.AccessTokenPrefix)
                    || item.Email.Contains(LocalGitHubFixture.AccessTokenPrefix)
                    || item.Username.Contains(LocalGitHubFixture.AccessTokenPrefix))
                .CountAsync();

            var identities = await dbContext.ExternalIdentities
                .Where(item => item.Subject.Contains(LocalGitHubFixture.AccessTokenPrefix))
                .CountAsync();

            var sessions = await dbContext.Sessions
                .Where(item => item.TokenHash.Contains(LocalGitHubFixture.AccessTokenPrefix))
                .CountAsync();

            var states = await dbContext.OAuthStates
                .Where(item => item.StateHash.Contains(LocalGitHubFixture.AccessTokenPrefix)
                    || item.BindingHash.Contains(LocalGitHubFixture.AccessTokenPrefix))
                .CountAsync();

            return users + identities + sessions + states;
        });

        Assert.Equal(0, leaked);
    }

    [Fact]
    public async Task IDENT1RejectionAReplayedStateCreatesNoSecondSession()
    {
        using var host = new IdentityTestHost(_fixture);

        var start = await host.StartSignInAsync("bob");
        var first = await host.CompleteSignInAsync(start.Callback);

        Assert.Equal(HttpStatusCode.Found, first.StatusCode);
        Assert.Equal("/onboarding", first.Headers.Location?.OriginalString);

        var sessionsAfterFirst = await host.CountSessionsAsync();
        Assert.True(sessionsAfterFirst > 0);

        // The same state a second time is refused and creates no session.
        var replay = await host.CompleteSignInAsync(start.Callback);

        Assert.Equal(HttpStatusCode.Found, replay.StatusCode);
        Assert.Equal("/sign-in?error=replayed_state", replay.Headers.Location?.OriginalString);

        Assert.Equal(sessionsAfterFirst, await host.CountSessionsAsync());
    }

    [Fact]
    public async Task IDENT1RejectionAnExpiredStateCreatesNoSession()
    {
        using var host = new IdentityTestHost(_fixture);

        var start = await host.StartSignInAsync("cara");
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(start.Callback.Query)["state"];

        Assert.False(string.IsNullOrWhiteSpace(state));

        var stateHash = SecretToken.Hash(state!);

        await host.WithDatabaseAsync(async dbContext =>
        {
            var row = await dbContext.OAuthStates.FirstAsync(item => item.StateHash == stateHash);
            row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
            return true;
        });

        var sessionsBefore = await host.CountSessionsAsync();

        var callback = await host.CompleteSignInAsync(start.Callback);

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal("/sign-in?error=expired_state", callback.Headers.Location?.OriginalString);

        Assert.Equal(sessionsBefore, await host.CountSessionsAsync());
    }

    [Fact]
    public async Task IDENT1RejectionAMismatchedPreAuthCookieCreatesNoSession()
    {
        using var host = new IdentityTestHost(_fixture);

        var start = await host.StartSignInAsync("dana");
        var sessionsBefore = await host.CountSessionsAsync();

        // A callback that arrives without the pre-auth cookie of the state is
        // refused, even though the state itself is valid.
        using var withoutCookie = host.CreateClientWithoutCookies();
        var refused = await withoutCookie.GetAsync(start.Callback.PathAndQuery, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Found, refused.StatusCode);
        Assert.Equal("/sign-in?error=mismatched_state", refused.Headers.Location?.OriginalString);

        Assert.Equal(sessionsBefore, await host.CountSessionsAsync());
    }

    [Fact]
    public async Task IDENT1BoundaryABannedAccountLosesItsAccessOnTheNextRequest()
    {
        using var host = new IdentityTestHost(_fixture);

        var callback = await host.SignInAsync("erin");

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);

        var before = await host.ReadSessionAsync();
        Assert.Equal(HttpStatusCode.OK, before.Response.StatusCode);

        using (var document = await host.ReadJsonAsync(before.Response))
        {
            Assert.True(document.RootElement.GetProperty("authenticated").GetBoolean());
            Assert.Equal(UserRole.Administrator, document.RootElement.GetProperty("user").GetProperty("role").GetString());
        }

        var user = await host.FindUserBySubjectAsync("1005");
        Assert.NotNull(user);

        await host.WithDatabaseAsync(async dbContext =>
        {
            var row = await dbContext.Users.FirstAsync(item => item.Id == user!.Id);
            row.IsBanned = true;
            await dbContext.SaveChangesAsync();
            return true;
        });

        var after = await host.ReadSessionAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, after.Response.StatusCode);

        var body = await after.Response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("fixture-access-token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blindmesh.test", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IDENT1TheLogoutRemovesTheServerSession()
    {
        using var host = new IdentityTestHost(_fixture);

        var callback = await host.SignInAsync("alice");
        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);

        var sessionsBefore = await host.CountSessionsAsync();
        Assert.True(sessionsBefore > 0);

        var logout = await host.SendUnsafeAsync(HttpMethod.Post, "/v1/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(sessionsBefore - 1, await host.CountSessionsAsync());

        var profile = await host.Client.GetAsync("/v1/profile", CancellationToken.None);
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    [Fact]
    public async Task IDENT1AnUnsafeRequestWithoutTheCsrfTokenOfTheSessionIsRefused()
    {
        using var host = new IdentityTestHost(_fixture);

        var (_, csrfToken) = await host.ReadSessionAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/logout");
        request.Headers.TryAddWithoutValidation("Origin", IdentityTestHost.Origin);
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", "not-the-token-of-the-session");

        var refused = await host.Client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var document = await host.ReadJsonAsync(refused);
        Assert.Equal("csrf_token_invalid", document.RootElement.GetProperty("error").GetProperty("code").GetString());

        var accepted = await host.SendUnsafeAsync(HttpMethod.Post, "/v1/logout", null, csrfToken);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    [Fact]
    public async Task IDENT1TheIdentityWritesNoOutboxMessageAndNoIdempotencyRecord()
    {
        using var host = new IdentityTestHost(_fixture);

        var outboxBefore = await host.WithDatabaseAsync(async dbContext => await dbContext.OutboxMessages.CountAsync());
        var idempotencyBefore = await host.WithDatabaseAsync(async dbContext => await dbContext.IdempotencyRecords.CountAsync());

        await host.SignInAsync("alice");

        var outboxAfter = await host.WithDatabaseAsync(async dbContext => await dbContext.OutboxMessages.CountAsync());
        var idempotencyAfter = await host.WithDatabaseAsync(async dbContext => await dbContext.IdempotencyRecords.CountAsync());

        // The identity module protects its commands with the one-use state and
        // the expected version, so it never uses the outbox or the
        // idempotency seam of the product.
        Assert.Equal(outboxBefore, outboxAfter);
        Assert.Equal(idempotencyBefore, idempotencyAfter);
    }
}
