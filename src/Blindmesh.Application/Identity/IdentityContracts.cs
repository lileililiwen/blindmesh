namespace Blindmesh.Application.Identity;

/// <summary>The signed in person of a session.</summary>
public sealed record SessionUserView(
    Guid Id,
    string Username,
    string DisplayName,
    string Role,
    string GithubLogin);

/// <summary>
/// The state of the session of the caller. The CSRF token is bound to the
/// server session and is only handed out here.
/// </summary>
public sealed record SessionView(
    bool Authenticated,
    string CsrfToken,
    DateTimeOffset ExpiresAt,
    SessionUserView? User);

/// <summary>The private profile of the signed in contributor.</summary>
public sealed record ProfileView(
    Guid Id,
    string Username,
    string DisplayName,
    string Bio,
    string Timezone,
    string[] Skills,
    string[] Languages,
    bool IsPublic,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// The public profile of a contributor. It never carries the email, the
/// provider identity, the session or the state of the account.
/// </summary>
public sealed record PublicProfileView(
    string Username,
    string DisplayName,
    string Bio,
    string Timezone,
    string[] Skills,
    string[] Languages,
    DateTimeOffset JoinedAt,
    int Version);

/// <summary>The address the browser is sent to for the sign in.</summary>
public sealed record SignInStartView(string AuthorizeUrl, int ExpiresInSeconds);

/// <summary>The server side ticket of an issued session.</summary>
public sealed record SessionTicket(
    Guid SessionId,
    Guid? UserId,
    string Token,
    string CsrfToken,
    DateTimeOffset ExpiresAt);

/// <summary>The session of a request, resolved from the cookie.</summary>
public sealed record SessionResolution(
    Guid SessionId,
    Guid? UserId,
    string Role,
    string Username,
    string DisplayName,
    string GithubLogin,
    bool IsBanned,
    DateTimeOffset ExpiresAt)
{
    public bool IsAuthenticated => UserId.HasValue;
}

/// <summary>The outcome of a profile change.</summary>
public enum UpdateProfileStatus
{
    Updated,
    ValidationFailed,
    VersionConflict,
    NotFound,
}

public sealed record UpdateProfileResult(
    UpdateProfileStatus Status,
    ProfileView? Profile = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    int? CurrentVersion = null);

/// <summary>The outcome of a state check of the OAuth callback.</summary>
public enum OAuthStateStatus
{
    Valid,
    NotFound,
    Expired,
    Replayed,
    Mismatched,
}

public sealed record OAuthStateResult(OAuthStateStatus Status, Guid StateId, string ReturnPath);

/// <summary>The state and the pre-auth binding of a started sign in.</summary>
public sealed record OAuthStart(string State, string Binding, DateTimeOffset ExpiresAt);

/// <summary>The outcome of a completed sign in.</summary>
public sealed record SignInOutcome(
    bool Succeeded,
    bool StateReplayed = false,
    Guid UserId = default,
    string Username = "",
    string DisplayName = "",
    string Role = "",
    SessionTicket? Ticket = null);
