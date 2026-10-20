namespace Blindmesh.Application.Identity;

/// <summary>
/// The identity of a person at the provider. The provider access token is
/// never part of this record, so it cannot be stored by accident.
/// </summary>
public sealed record ProviderIdentity(
    string Provider,
    string Subject,
    string Login,
    string Email,
    string? InitialRole = null);

/// <summary>
/// The GitHub authorization code flow. The API owns the flow: it sends the
/// browser to the provider, exchanges the code and reads the identity. The
/// access token is used for the identity read and then dropped.
/// </summary>
public interface IGitHubIdentityClient
{
    /// <summary>True when the client can be used right now.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// The address the browser is sent to. The state is echoed by the
    /// provider and read back by the callback.
    /// </summary>
    string BuildAuthorizeUrl(string state, string redirectUri, string? localActor);

    /// <summary>
    /// Exchanges the authorization code for the identity of the account. The
    /// access token of the provider is used here and never returned.
    /// </summary>
    Task<ProviderIdentity> ExchangeAsync(string code, string redirectUri, CancellationToken cancellationToken);
}
