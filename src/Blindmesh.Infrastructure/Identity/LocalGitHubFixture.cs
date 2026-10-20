using Blindmesh.Application.Identity;
using Blindmesh.Domain.Identity;

namespace Blindmesh.Infrastructure.Identity;

/// <summary>
/// A seeded account of the local OAuth fixture. The fixture is the test
/// double of the identity provider; it never runs in production. The age of
/// the account counts from the moment of the sign in, so the seeded accounts
/// have a real age and not a date of the past.
/// </summary>
public sealed record FixtureActor(
    string Key,
    string Subject,
    string Login,
    string Email,
    string Role,
    int AccountAgeDays,
    bool EmailVerified);

/// <summary>
/// The local GitHub fixture. It serves the seeded accounts of the product so
/// the sign in journey can be exercised without a provider and without a
/// credential of a real account.
/// </summary>
public static class LocalGitHubFixture
{
    /// <summary>
    /// The prefix of every access token the fixture mints. The tests use it
    /// to prove that no provider token is stored in the database.
    /// </summary>
    public const string AccessTokenPrefix = "fixture-access-token";

    public static readonly IReadOnlyList<FixtureActor> Actors =
    [
        new("alice", "1001", "alice", "alice@blindmesh.test", UserRole.User, 400, true),
        new("bob", "1002", "bob", "bob@blindmesh.test", UserRole.User, 30, true),
        new("cara", "1003", "cara", "cara@blindmesh.test", UserRole.User, 1, false),
        new("dana", "1004", "dana", "dana@blindmesh.test", UserRole.Moderator, 120, true),
        new("erin", "1005", "erin", "erin@blindmesh.test", UserRole.Administrator, 200, true),
    ];

    public static FixtureActor? Find(string? key) =>
        key is null
            ? null
            : Actors.FirstOrDefault(actor => string.Equals(actor.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Mints an access token like the provider would.</summary>
    public static string MintAccessToken(FixtureActor actor) =>
        $"{AccessTokenPrefix}-{actor.Key}-{Guid.NewGuid():N}";
}

/// <summary>
/// The local client of the fixture. It answers the exchange of the
/// authorization code with the seeded account. The access token is minted and
/// dropped here; it is never stored and never leaves the process.
/// </summary>
public sealed class LocalGitHubFixtureClient(IdentityOptions options) : IGitHubIdentityClient
{
    public bool IsConfigured => true;

    public string BuildAuthorizeUrl(string state, string redirectUri, string? localActor)
    {
        var actor = LocalGitHubFixture.Find(localActor) ?? LocalGitHubFixture.Actors[0];

        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = "local-fixture",
            ["redirect_uri"] = redirectUri,
            ["scope"] = options.GitHub.Scope,
            ["state"] = state,
            ["response_type"] = "code",
            ["actor"] = actor.Key,
        };

        var url = $"{options.PublicBaseUrl.TrimEnd('/')}/{options.BrowserApiPrefix.Trim('/')}/test/github/authorize";

        return $"{url}?{string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))}";
    }

    public Task<ProviderIdentity> ExchangeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var actor = LocalGitHubFixture.Find(code)
            ?? throw new InvalidOperationException("The local OAuth fixture does not know this authorization code.");

        // The access token of the provider is minted and dropped with this
        // scope. It is never stored and never returned to the caller.
        var accessToken = LocalGitHubFixture.MintAccessToken(actor);
        _ = accessToken;

        // The provider only hands out a verified primary email. An account
        // without one has no email address, exactly like the real provider.
        var email = actor.EmailVerified ? actor.Email : string.Empty;

        return Task.FromResult(new ProviderIdentity(
            IdentityProviders.GitHub,
            actor.Subject,
            actor.Login,
            email,
            actor.Role,
            DateTimeOffset.UtcNow.AddDays(-actor.AccountAgeDays)));
    }
}
