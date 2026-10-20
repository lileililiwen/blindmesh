using Blindmesh.Application.Abstractions;
using Blindmesh.Domain.Identity;

namespace Blindmesh.Application.Identity;

/// <summary>
/// The identity service of the product. It owns the session, the OAuth state
/// and the contributor profile. Every write goes through the store of the
/// infrastructure and every command that changes more than one row runs in
/// the command transaction of the product.
/// </summary>
public sealed class IdentityService(
    IIdentityStore store,
    IClock clock,
    ICommandTransactionFactory transactions,
    IGitHubIdentityClient gitHub)
{
    /// <summary>The session is refused after seven days without activity.</summary>
    public const int IdleLifetimeDays = 7;

    /// <summary>The session is refused after thirty days, whatever the activity.</summary>
    public const int AbsoluteLifetimeDays = 30;

    /// <summary>An OAuth state is valid for ten minutes.</summary>
    public const int StateLifetimeMinutes = 10;

    private static readonly TimeSpan ActivityWriteThreshold = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Issues a session and its CSRF token. The raw token is only returned
    /// here; the store keeps its hash.
    /// </summary>
    public async Task<SessionTicket> IssueSessionAsync(Guid? userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var (session, token, csrfToken) = NewSession(userId, now);

        await store.AddSessionAsync(session, cancellationToken);

        return new SessionTicket(session.Id, userId, token, csrfToken, session.ExpiresAt);
    }

    /// <summary>
    /// Resolves the session of a request. Null means that there is no usable
    /// session. A banned account is reported, so the caller can refuse the
    /// request instead of pretending that the visitor is anonymous.
    /// </summary>
    public async Task<SessionResolution?> ResolveSessionAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var session = await store.FindSessionByTokenHashAsync(SecretToken.Hash(rawToken), cancellationToken);

        if (session is null)
        {
            return null;
        }

        var now = clock.UtcNow;

        if (session.RevokedAt is not null || now > session.ExpiresAt || now > session.LastSeenAt.AddDays(IdleLifetimeDays))
        {
            return null;
        }

        if (session.UserId is null)
        {
            await TouchSessionAsync(session, now, cancellationToken);

            return new SessionResolution(
                session.Id,
                null,
                UserRole.User,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                session.ExpiresAt);
        }

        var user = await store.FindUserAsync(session.UserId.Value, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.IsBanned)
        {
            // A ban takes effect on the next request: the session is closed
            // and the request is refused.
            session.RevokedAt = now;
            await store.SaveSessionAsync(session, cancellationToken);

            return new SessionResolution(
                session.Id,
                user.Id,
                user.Role,
                user.Username,
                user.DisplayName,
                user.GithubLogin,
                true,
                session.ExpiresAt);
        }

        await TouchSessionAsync(session, now, cancellationToken);

        return new SessionResolution(
            session.Id,
            user.Id,
            user.Role,
            user.Username,
            user.DisplayName,
            user.GithubLogin,
            false,
            session.ExpiresAt);
    }

    /// <summary>Removes the server session of a sign out.</summary>
    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await store.RemoveSessionAsync(sessionId, cancellationToken);
    }

    /// <summary>The private profile of the signed in contributor.</summary>
    public async Task<ProfileView?> GetOwnProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await store.FindUserAsync(userId, cancellationToken);

        return user is null ? null : ToProfileView(user);
    }

    /// <summary>
    /// The public profile of a contributor. A missing, private or banned
    /// account is not readable and the caller answers the safe 404.
    /// </summary>
    public async Task<PublicProfileView?> GetPublicProfileAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = ProfileRules.NormalizeUsername(username);
        var user = await store.FindUserByUsernameAsync(normalized, cancellationToken);

        if (user is null || !user.IsPublic || user.IsBanned)
        {
            return null;
        }

        return new PublicProfileView(
            user.Username,
            user.DisplayName,
            user.Bio,
            user.Timezone,
            user.Skills,
            user.Languages,
            user.CreatedAt,
            user.Version);
    }

    /// <summary>
    /// Saves the profile of the signed in contributor. The expected version
    /// protects the profile against a lost change. A field that the caller
    /// left out keeps its current value.
    /// </summary>
    public async Task<UpdateProfileResult> UpdateProfileAsync(
        Guid actorId,
        int expectedVersion,
        ProfilePatch patch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var user = await store.FindUserAsync(actorId, cancellationToken);

        if (user is null)
        {
            return new UpdateProfileResult(UpdateProfileStatus.NotFound);
        }

        if (user.Version != expectedVersion)
        {
            return new UpdateProfileResult(UpdateProfileStatus.VersionConflict, CurrentVersion: user.Version);
        }

        var input = new ProfileInput
        {
            Username = patch.Username ?? user.Username,
            DisplayName = patch.DisplayName ?? user.DisplayName,
            Bio = patch.Bio ?? user.Bio,
            Timezone = patch.Timezone ?? user.Timezone,
            Skills = patch.Skills ?? user.Skills,
            Languages = patch.Languages ?? user.Languages,
            IsPublic = patch.IsPublic ?? user.IsPublic,
        };

        var fieldErrors = ProfileRules.Validate(input);

        if (fieldErrors.Count > 0)
        {
            return new UpdateProfileResult(UpdateProfileStatus.ValidationFailed, FieldErrors: fieldErrors);
        }

        var username = ProfileRules.NormalizeUsername(input.Username);

        if (!string.Equals(username, user.Username, StringComparison.Ordinal) &&
            await store.IsUsernameTakenAsync(username, actorId, cancellationToken))
        {
            return new UpdateProfileResult(
                UpdateProfileStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["username"] = ["That username is already taken."],
                });
        }

        user.Username = username;
        user.DisplayName = input.DisplayName.Trim();
        user.Bio = input.Bio.Trim();
        user.Timezone = input.Timezone;
        user.Skills = input.Skills.Select(tag => tag.Trim()).ToArray();
        user.Languages = input.Languages.Select(tag => tag.Trim()).ToArray();
        user.IsPublic = input.IsPublic;
        user.Version += 1;
        user.UpdatedAt = clock.UtcNow;

        if (!await store.TrySaveUserAsync(user, cancellationToken))
        {
            var current = await store.FindUserAsync(actorId, cancellationToken);

            return new UpdateProfileResult(UpdateProfileStatus.VersionConflict, CurrentVersion: current?.Version);
        }

        return new UpdateProfileResult(UpdateProfileStatus.Updated, Profile: ToProfileView(user));
    }

    /// <summary>
    /// Starts a sign in. The state is random, bound to a pre-auth cookie and
    /// valid for ten minutes.
    /// </summary>
    public async Task<OAuthStart> CreateOAuthStateAsync(string? returnPath, CancellationToken cancellationToken)
    {
        var state = SecretToken.Create();
        var binding = SecretToken.Create();
        var now = clock.UtcNow;

        var row = new OAuthState
        {
            Id = Guid.NewGuid(),
            StateHash = SecretToken.Hash(state),
            BindingHash = SecretToken.Hash(binding),
            ReturnPath = ReturnPathPolicy.Resolve(returnPath),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(StateLifetimeMinutes),
        };

        await store.AddOAuthStateAsync(row, cancellationToken);

        return new OAuthStart(state, binding, row.ExpiresAt);
    }

    /// <summary>
    /// Checks the state of a callback. The state is only read here; it is
    /// consumed with the session in one transaction.
    /// </summary>
    public async Task<OAuthStateResult> CheckOAuthStateAsync(
        string? state,
        string? binding,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return new OAuthStateResult(OAuthStateStatus.NotFound, Guid.Empty, ReturnPathPolicy.Default);
        }

        var row = await store.FindOAuthStateAsync(SecretToken.Hash(state), cancellationToken);

        if (row is null)
        {
            return new OAuthStateResult(OAuthStateStatus.NotFound, Guid.Empty, ReturnPathPolicy.Default);
        }

        if (row.ConsumedAt is not null)
        {
            return new OAuthStateResult(OAuthStateStatus.Replayed, row.Id, row.ReturnPath);
        }

        if (clock.UtcNow > row.ExpiresAt)
        {
            return new OAuthStateResult(OAuthStateStatus.Expired, row.Id, row.ReturnPath);
        }

        if (string.IsNullOrWhiteSpace(binding) ||
            !SecretToken.Matches(row.BindingHash, binding))
        {
            return new OAuthStateResult(OAuthStateStatus.Mismatched, row.Id, row.ReturnPath);
        }

        return new OAuthStateResult(OAuthStateStatus.Valid, row.Id, row.ReturnPath);
    }

    /// <summary>
    /// Finishes a sign in. The state is consumed, the user and the provider
    /// identity are stored and the session is issued in one transaction, so a
    /// failure never leaves a half finished sign in behind.
    /// </summary>
    public async Task<SignInOutcome> CompleteSignInAsync(
        ProviderIdentity identity,
        Guid stateId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        await using var transaction = await transactions.BeginAsync(cancellationToken);
        var now = clock.UtcNow;

        if (!await store.TryConsumeOAuthStateAsync(stateId, now, cancellationToken))
        {
            // Another request used the state first. No session is created.
            return new SignInOutcome(false, StateReplayed: true);
        }

        var user = await store.FindUserByExternalIdentityAsync(identity.Provider, identity.Subject, cancellationToken);

        if (user is null)
        {
            user = await CreateUserAsync(identity, now, cancellationToken);

            if (user is null)
            {
                return new SignInOutcome(false);
            }
        }
        else
        {
            await UpdateProviderFieldsAsync(user, identity, now, cancellationToken);
            await store.UpdateExternalAccountCreatedAtAsync(user.Id, identity.AccountCreatedAt, cancellationToken);
        }

        var (session, token, csrfToken) = NewSession(user.Id, now);
        await store.AddSessionAsync(session, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SignInOutcome(
            true,
            false,
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            new SessionTicket(session.Id, user.Id, token, csrfToken, session.ExpiresAt));
    }

    /// <summary>The address the browser is sent to for the sign in.</summary>
    public string BuildAuthorizeUrl(string state, string redirectUri, string? localActor) =>
        gitHub.BuildAuthorizeUrl(state, redirectUri, localActor);

    /// <summary>True when the provider can be used right now.</summary>
    public bool IsProviderConfigured => gitHub.IsConfigured;

    /// <summary>
    /// Reads the identity of the provider account. The access token of the
    /// provider is used here and never leaves the client.
    /// </summary>
    public Task<ProviderIdentity> ExchangeAsync(string code, string redirectUri, CancellationToken cancellationToken) =>
        gitHub.ExchangeAsync(code, redirectUri, cancellationToken);

    private static ProfileView ToProfileView(User user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Bio,
            user.Timezone,
            user.Skills,
            user.Languages,
            user.IsPublic,
            user.Version,
            user.CreatedAt,
            user.UpdatedAt);

    private static (UserSession Session, string Token, string CsrfToken) NewSession(Guid? userId, DateTimeOffset now)
    {
        var token = SecretToken.Create();
        var csrfToken = SecretToken.DeriveCsrfToken(token);

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = SecretToken.Hash(token),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(AbsoluteLifetimeDays),
        };

        return (session, token, csrfToken);
    }

    private async Task TouchSessionAsync(UserSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (now - session.LastSeenAt < ActivityWriteThreshold)
        {
            return;
        }

        session.LastSeenAt = now;
        await store.SaveSessionAsync(session, cancellationToken);
    }

    private async Task<User?> CreateUserAsync(ProviderIdentity identity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var suggestion = ProfileRules.SuggestUsername(identity.Login);

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var candidate = attempt == 1 ? suggestion : WithSuffix(suggestion, attempt);

            if (!ProfileRules.IsValidUsername(candidate))
            {
                continue;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = candidate,
                DisplayName = identity.Login,
                GithubLogin = identity.Login,
                Email = identity.Email,
                Timezone = "UTC",
                Skills = [],
                Languages = [],
                Role = UserRole.IsKnown(identity.InitialRole) ? identity.InitialRole! : UserRole.User,
                IsPublic = true,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };

            var external = new ExternalIdentity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = identity.Provider,
                Subject = identity.Subject,
                AccountCreatedAt = identity.AccountCreatedAt,
                CreatedAt = now,
            };

            if (await store.TryAddUserAsync(user, external, cancellationToken))
            {
                return user;
            }

            // Another request may have created the same provider identity.
            var existing = await store.FindUserByExternalIdentityAsync(identity.Provider, identity.Subject, cancellationToken);

            if (existing is not null)
            {
                return existing;
            }
        }

        return null;
    }

    private async Task UpdateProviderFieldsAsync(User user, ProviderIdentity identity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.Equals(user.GithubLogin, identity.Login, StringComparison.Ordinal) &&
            string.Equals(user.Email, identity.Email, StringComparison.Ordinal))
        {
            return;
        }

        user.GithubLogin = identity.Login;
        user.Email = identity.Email;
        user.UpdatedAt = now;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await store.TrySaveUserAsync(user, cancellationToken))
            {
                return;
            }

            var current = await store.FindUserAsync(user.Id, cancellationToken);

            if (current is null)
            {
                return;
            }

            current.GithubLogin = identity.Login;
            current.Email = identity.Email;
            current.UpdatedAt = now;
            user = current;
        }
    }

    private static string WithSuffix(string username, int attempt)
    {
        var suffix = $"-{attempt}";
        var room = ProfileRules.UsernameMaxLength - suffix.Length;
        var stem = username.Length > room ? username[..room].TrimEnd('-') : username;

        return $"{stem}{suffix}";
    }
}
