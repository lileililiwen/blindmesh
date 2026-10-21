namespace Blindmesh.Domain.Moderation;

/// <summary>
/// A report of visible abusive content. The row keeps the private identity of
/// the reporter next to the body of the report. Only authorized moderation
/// staff may read the body, and the identity of the reporter never leaves the
/// moderation module.
/// </summary>
public sealed class Report
{
    public Guid Id { get; set; }

    /// <summary>The account that wrote the report. It is never part of an answer.</summary>
    public Guid ReporterId { get; set; }

    /// <summary>
    /// The receipt of the report. It is a stable code the reporter reads back
    /// to follow the case without learning anything about the reporter.
    /// </summary>
    public string Receipt { get; set; } = string.Empty;

    /// <summary>The body of the report. Only moderation staff may read it.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>The kind of the content the report names, for example submission.</summary>
    public string SubjectType { get; set; } = string.Empty;

    /// <summary>The content the report names, when it names one.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>The state of the report.</summary>
    public ReportState State { get; set; } = ReportState.Open;

    /// <summary>The moderator that read the report.</summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>When the report was read.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>The answer of the moderation, when the report was answered.</summary>
    public string? Resolution { get; set; }

    /// <summary>The optimistic concurrency version of the report.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
