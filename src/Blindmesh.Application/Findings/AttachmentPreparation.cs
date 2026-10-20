namespace Blindmesh.Application.Findings;

/// <summary>
/// The outcome of the check of the bytes of a private file. The check runs
/// outside the transaction of the command, because it reads the bytes of the
/// file and it writes them into the private object storage.
/// </summary>
public sealed record AttachmentPreparation(bool Passed, string Reason, string Sha256)
{
    /// <summary>The file passed the check of the pipeline.</summary>
    public static AttachmentPreparation Accepted(string sha256) => new(true, string.Empty, sha256);

    /// <summary>The file was refused by the check of the pipeline.</summary>
    public static AttachmentPreparation Refused(string reason) => new(false, reason, string.Empty);
}

/// <summary>
/// The private object storage is not reachable. The command answers with the
/// state of the service and it never leaks the detail of the provider.
/// </summary>
public sealed class ObjectStorageUnavailableException()
    : Exception("The private object storage is not reachable.");
