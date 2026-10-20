namespace Blindmesh.Domain.Credits;

/// <summary>
/// The claim of the limited grant of one provider subject. The provider and
/// the subject are unique, so a subject can claim the grant only once. The
/// claim keeps the provenance of the issued credits: the account, the
/// transaction, the moment of the claim and the expiry of the credits.
/// </summary>
public sealed class GrantClaim
{
    public Guid Id { get; set; }

    /// <summary>The provider of the account, for example github.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The stable subject of the account at the provider.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The contributor that received the grant.</summary>
    public Guid UserId { get; set; }

    /// <summary>The ordinary account of the contributor that received the grant.</summary>
    public Guid AccountId { get; set; }

    /// <summary>The transaction that issued the grant.</summary>
    public Guid TransactionId { get; set; }

    /// <summary>The issued amount in credits.</summary>
    public long Amount { get; set; }

    /// <summary>
    /// The address the claim came from. It is only kept to hold the daily
    /// limit of the claims of one address, and it is never part of an answer.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>The moment the contributor accepted the rules of the marketplace.</summary>
    public DateTimeOffset RulesAcceptedAt { get; set; }

    /// <summary>The moment the grant was issued.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The moment the issued credits expire.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
