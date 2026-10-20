namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The deadlines of a participation. They are the deadlines of the shared
/// architecture: a contributor has 48 hours to apply, an accepted contributor
/// has 24 hours to start and the execution of a started participation is
/// bounded by seven days. The visible deadline of the execution is the
/// smaller of the deadline of the task and the start plus seven days.
/// </summary>
public static class ParticipationRules
{
    /// <summary>The hours a contributor has to apply after the publication of the task.</summary>
    public const int ApplyWindowHours = 48;

    /// <summary>The hours an owner has to decide about an application.</summary>
    public const int ApplicationDecisionHours = 48;

    /// <summary>The hours an accepted contributor has to start the work.</summary>
    public const int StartWindowHours = 24;

    /// <summary>The days the execution of a started participation may take.</summary>
    public const int ExecutionDays = 7;

    /// <summary>
    /// The hours a running participation keeps its submission grace after its
    /// execution deadline.
    /// </summary>
    public const int SubmissionGraceHours = 24;

    /// <summary>The moment the task stops accepting applications.</summary>
    public static DateTimeOffset ApplyDeadline(DateTimeOffset publishedAt) =>
        publishedAt.AddHours(ApplyWindowHours);

    /// <summary>The moment an application stops waiting for a decision.</summary>
    public static DateTimeOffset DecisionDeadline(DateTimeOffset appliedAt) =>
        appliedAt.AddHours(ApplicationDecisionHours);

    /// <summary>The moment an accepted participation stops waiting for the start.</summary>
    public static DateTimeOffset StartDeadline(DateTimeOffset acceptedAt) =>
        acceptedAt.AddHours(StartWindowHours);

    /// <summary>
    /// The visible deadline of the execution of a participation. It is the
    /// smaller of the deadline of the task and the start plus seven days.
    /// </summary>
    public static DateTimeOffset ExecutionDeadline(
        DateTimeOffset startedAt,
        DateTimeOffset? taskDeadline)
    {
        var bounded = startedAt.AddDays(ExecutionDays);

        return taskDeadline is DateTimeOffset deadline && deadline < bounded
            ? deadline
            : bounded;
    }

    /// <summary>
    /// The moment a running participation loses its submission grace and may
    /// be expired.
    /// </summary>
    public static DateTimeOffset GraceDeadline(DateTimeOffset executionDeadline) =>
        executionDeadline.AddHours(SubmissionGraceHours);
}
