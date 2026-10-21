namespace Blindmesh.Domain.Moderation;

/// <summary>
/// One line of the trail of the moderation module. The row is append only: a
/// decision, a refusal and a compensation never change the lines that came
/// before them.
/// </summary>
public sealed class ModerationAudit
{
    public Guid Id { get; set; }

    /// <summary>The case the line belongs to, when it belongs to one.</summary>
    public Guid? DisputeId { get; set; }

    /// <summary>The report the line belongs to, when it belongs to one.</summary>
    public Guid? ReportId { get; set; }

    /// <summary>The account that caused the line.</summary>
    public Guid ActorId { get; set; }

    /// <summary>The action of the line, for example dispute.opened.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The reason of the line.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Free details of the line. It never holds a secret.</summary>
    public string? Details { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
