namespace Blindmesh.Domain.Operations;

/// <summary>
/// One deletion request of an operator. The request names the account whose
/// identity is removed or pseudonymized.
///
/// The row never holds the identity of the subject. It holds the pseudonym the
/// subject keeps, so the settled financial records and the audit rows stay
/// readable without the identity of the person.
/// </summary>
public sealed class DeletionRequest
{
    public Guid Id { get; set; }

    /// <summary>The operator that handled the request.</summary>
    public Guid RequesterId { get; set; }

    /// <summary>The account the request is about.</summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>The scope of the request, for example the profile and the sessions.</summary>
    public string Scope { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>The state of the request.</summary>
    public DeletionRequestStatus Status { get; set; }

    /// <summary>The pseudonym the subject keeps after the deletion.</summary>
    public string PseudonymizedUserId { get; set; } = string.Empty;

    /// <summary>The reference of the audit line of the deletion.</summary>
    public string AuditReference { get; set; } = string.Empty;
}
