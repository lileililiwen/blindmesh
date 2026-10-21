namespace Blindmesh.Domain.Moderation;

/// <summary>
/// The rules of the moderation package. They are the limits of the design of
/// the package: the length of a reason, the window of an appeal and the number
/// of appeals one case may carry.
/// </summary>
public static class ModerationRules
{
    /// <summary>The shortest reason of a report, of a dispute or of a decision.</summary>
    public const int ReasonMinLength = 20;

    /// <summary>The longest reason of a report, of a dispute or of a decision.</summary>
    public const int ReasonMaxLength = 5000;

    /// <summary>The days a party may appeal a resolution.</summary>
    public const int AppealWindowDays = 7;

    /// <summary>The number of appeals one case may carry.</summary>
    public const int MaxAppeals = 1;

    /// <summary>The longest reason of a ban.</summary>
    public const int BanReasonMaxLength = 1000;

    /// <summary>The moment an appeal of a resolution is due.</summary>
    public static DateTimeOffset AppealDeadline(DateTimeOffset resolvedAt) =>
        resolvedAt.AddDays(AppealWindowDays);
}
