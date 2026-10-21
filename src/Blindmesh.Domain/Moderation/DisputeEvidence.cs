namespace Blindmesh.Domain.Moderation;

/// <summary>
/// One piece of evidence of a case. It is written by the moderation module
/// when a moderator asks the parties for more, and it is append only.
/// </summary>
public sealed class DisputeEvidence
{
    public Guid Id { get; set; }

    /// <summary>The case the evidence belongs to.</summary>
    public Guid DisputeId { get; set; }

    /// <summary>The account that wrote the evidence.</summary>
    public Guid SubmittedBy { get; set; }

    /// <summary>The kind of the evidence: a request of a moderator or an answer of a party.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The body of the evidence.</summary>
    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
