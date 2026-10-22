namespace Blindmesh.Domain.Operations;

/// <summary>
/// The state of one deletion request. A request is received first, then it is
/// completed or refused.
/// </summary>
public enum DeletionRequestStatus
{
    /// <summary>The request was received and waits for the operator.</summary>
    Received,

    /// <summary>The identity of the subject was removed and the pseudonym was kept.</summary>
    Completed,

    /// <summary>The request was refused. The identity of the subject is untouched.</summary>
    Refused,
}
