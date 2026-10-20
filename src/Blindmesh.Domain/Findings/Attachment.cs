using System.ComponentModel.DataAnnotations;

namespace Blindmesh.Domain.Findings;

/// <summary>
/// A private file of the evidence of a contributor. An attachment belongs to one
/// revision of one submission and is readable by the contributor and by the
/// owner of the task only. It never holds a credential of a resource: an
/// attachment is a text, a log or a picture of the work.
/// </summary>
public sealed class Attachment
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    /// <summary>The revision the attachment belongs to.</summary>
    public Guid RevisionId { get; set; }

    /// <summary>The contributor that uploaded the file.</summary>
    public Guid ContributorId { get; set; }

    /// <summary>The name of the file. It never holds a path of the contributor.</summary>
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>The declared content type of the file.</summary>
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>The size of the file in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>The key of the object in the private object storage.</summary>
    [MaxLength(400)]
    public string StorageKey { get; set; } = string.Empty;

    public AttachmentState State { get; set; } = AttachmentState.Quarantined;

    /// <summary>The reason the check of the pipeline refused the file.</summary>
    [MaxLength(300)]
    public string? RejectionReason { get; set; }

    /// <summary>The digest of the bytes of the file. It is empty while the file waits.</summary>
    [MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The moment the check of the pipeline released the file.</summary>
    public DateTimeOffset? ScannedAt { get; set; }
}
