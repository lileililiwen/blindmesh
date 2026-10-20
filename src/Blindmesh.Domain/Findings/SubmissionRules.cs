namespace Blindmesh.Domain.Findings;

/// <summary>
/// The rules of a submission. They are the limits of the design of the
/// package: the length of the evidence, the length of a finding, the one
/// correction the owner may ask for and the deadlines of the review.
/// </summary>
public static class SubmissionRules
{
    /// <summary>The shortest summary of a submission.</summary>
    public const int SummaryMinLength = 1;

    /// <summary>The longest summary of a submission.</summary>
    public const int SummaryMaxLength = 5000;

    /// <summary>The longest title of a finding.</summary>
    public const int FindingTitleMaxLength = 140;

    /// <summary>The longest reproduction of a finding.</summary>
    public const int FindingReproductionMaxLength = 10000;

    /// <summary>The longest expected and actual result of a finding.</summary>
    public const int FindingResultMaxLength = 5000;

    /// <summary>The longest note of the owner about a decision.</summary>
    public const int DecisionNoteMaxLength = 1000;

    /// <summary>The longest reason of a dispute.</summary>
    public const int DisputeReasonMaxLength = 2000;

    /// <summary>The longest explanation of a clean result.</summary>
    public const int CleanResultMaxLength = 5000;

    /// <summary>The number of findings one revision may carry.</summary>
    public const int MaxFindingsPerRevision = 50;

    /// <summary>The number of tested areas one revision may carry.</summary>
    public const int MaxTestedAreas = 50;

    /// <summary>The hours the contributor has to answer the one correction.</summary>
    public const int CorrectionWindowHours = 48;

    /// <summary>
    /// The hours the owner has to review the evidence. After that the base of
    /// the seat is accepted automatically, when no correction and no dispute is
    /// open.
    /// </summary>
    public const int ReviewTimeoutHours = 72;

    /// <summary>The seconds a signed address of a file stays valid.</summary>
    public const int DownloadUrlSeconds = 60;

    /// <summary>The moment the correction of the contributor is due.</summary>
    public static DateTimeOffset CorrectionDeadline(DateTimeOffset requestedAt) =>
        requestedAt.AddHours(CorrectionWindowHours);

    /// <summary>The moment the review of the owner is due.</summary>
    public static DateTimeOffset ReviewDeadline(DateTimeOffset submittedAt) =>
        submittedAt.AddHours(ReviewTimeoutHours);
}
