namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The rules of a task. They live in the domain, so the service, the API, the
/// interface and the tests agree on one definition of a valid task and of a
/// publishable task.
/// </summary>
public static class TaskRules
{
    public const int TitleMinLength = 1;

    public const int TitleMaxLength = 140;

    public const int ScopeMaxLength = 10_000;

    public const int ExpectedMinutesMin = 5;

    public const int ExpectedMinutesMax = 480;

    public const long BaseRewardMin = 1;

    public const long BaseRewardMax = 10_000;

    public const long SlotBonusMin = 0;

    public const long SlotBonusMax = 10_000;

    public const int SlotCountMin = 1;

    public const int SlotCountMax = 20;

    /// <summary>The shortest time between the publication and the deadline.</summary>
    public const int DeadlineMinHours = 24;

    /// <summary>The longest time between the publication and the deadline.</summary>
    public const int DeadlineMaxDays = 30;

    public const int RequirementValueMaxLength = 100;

    public const int StepTitleMaxLength = 200;

    public const int StepExpectedResultMaxLength = 2_000;

    public const int AccessResourceMaxLength = 500;

    public const int ReviewCriteriaMaxLength = 2_000;

    public const int MaxSteps = 20;

    public const int MaxRequirements = 20;

    /// <summary>
    /// The kinds a hard requirement may use. They are the kinds a contributor
    /// offers and that a task may require: a region, a device or a role.
    /// </summary>
    public static IReadOnlyList<string> RequirementKinds { get; } = ["region", "device", "role"];

    /// <summary>
    /// The maximum budget of a task: the base reward and the bonus of every
    /// slot. The whole amount is reserved when the task is published.
    /// </summary>
    public static long MaximumBudget(long baseReward, long slotBonus, int slotCount) =>
        (baseReward + slotBonus) * slotCount;

    /// <summary>
    /// Checks every field of a task. The result holds one entry per invalid
    /// field, so the interface can attach the message to the field. The rules
    /// of the modes are checked by the publication, so a draft may be saved
    /// before it is complete.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Validate(TaskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = [];
                errors[field] = messages;
            }

            messages.Add(message);
        }

        var title = input.Title.Trim();

        if (title.Length is < TitleMinLength or > TitleMaxLength)
        {
            Add("title", $"The title must be {TitleMinLength} to {TitleMaxLength} characters long.");
        }

        if (input.Scope.Trim().Length > ScopeMaxLength)
        {
            Add("scope", $"The scope must be at most {ScopeMaxLength} characters long.");
        }

        if (input.Modes == TaskMode.None)
        {
            Add("modes", "Choose at least one mode: explore, verify, access or review.");
        }

        if (input.ExpectedMinutes is < ExpectedMinutesMin or > ExpectedMinutesMax)
        {
            Add("expected_minutes", $"The expected minutes must be {ExpectedMinutesMin} to {ExpectedMinutesMax}.");
        }

        if (input.BaseReward is < BaseRewardMin or > BaseRewardMax)
        {
            Add("base_reward", $"The base reward must be {BaseRewardMin} to {BaseRewardMax} credits.");
        }

        if (input.SlotBonus is < SlotBonusMin or > SlotBonusMax)
        {
            Add("slot_bonus", $"The bonus must be {SlotBonusMin} to {SlotBonusMax} credits.");
        }

        if (input.SlotCount is < SlotCountMin or > SlotCountMax)
        {
            Add("slot_count", $"The number of slots must be {SlotCountMin} to {SlotCountMax}.");
        }

        if (input.Requirements.Count > MaxRequirements)
        {
            Add("requirements", $"A task carries at most {MaxRequirements} requirements.");
        }

        for (var index = 0; index < input.Requirements.Count; index++)
        {
            var requirement = input.Requirements[index];
            var kind = requirement.Kind.Trim().ToLowerInvariant();

            if (!RequirementKinds.Contains(kind, StringComparer.Ordinal))
            {
                Add("requirements", $"A requirement names the kind region, device or role.");
                continue;
            }

            if (requirement.Value.Trim().Length is < 1 or > RequirementValueMaxLength)
            {
                Add("requirements", $"A requirement names a value of 1 to {RequirementValueMaxLength} characters.");
            }
        }

        if (input.Steps.Count > MaxSteps)
        {
            Add("steps", $"A task carries at most {MaxSteps} steps.");
        }

        for (var index = 0; index < input.Steps.Count; index++)
        {
            var step = input.Steps[index];

            if (step.Title.Trim().Length is < 1 or > StepTitleMaxLength)
            {
                Add("steps", $"A step names a title of 1 to {StepTitleMaxLength} characters.");
            }

            if (step.ExpectedResult.Trim().Length is < 1 or > StepExpectedResultMaxLength)
            {
                Add("steps", $"A step names an expected result of 1 to {StepExpectedResultMaxLength} characters.");
            }
        }

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Checks the acceptance rules of the modes of a task. A verify task needs
    /// steps with an expected result, an explore task accepts a clean result,
    /// an access task needs a resource of the owner or a delegated consent and
    /// a review task accepts a structured peer assessment.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> ValidateAcceptance(TaskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        ValidateAcceptance(input, (field, message) =>
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = [];
                errors[field] = messages;
            }

            messages.Add(message);
        });

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static void ValidateAcceptance(TaskInput input, Action<string, string> add)
    {
        if (input.Modes.HasFlag(TaskMode.Explore) && !input.AcceptsCleanFindings)
        {
            add("accepts_clean_findings", "An explore task accepts a clean result. Say so.");
        }

        if (input.Modes.HasFlag(TaskMode.Verify))
        {
            var complete = input.Steps.Count > 0 &&
                input.Steps.All(step =>
                    step.Title.Trim().Length > 0 && step.ExpectedResult.Trim().Length > 0);

            if (!complete)
            {
                add("steps", "A verify task needs at least one step with an expected result.");
            }
        }

        if (input.Modes.HasFlag(TaskMode.Access))
        {
            var resource = input.AccessResource?.Trim() ?? string.Empty;

            if (input.AccessKind is null)
            {
                add("access_kind", "An access task names a resource of the owner or a delegated consent.");
            }
            else if (resource.Length is < 1 or > AccessResourceMaxLength)
            {
                add("access_resource", $"The access names a resource of 1 to {AccessResourceMaxLength} characters.");
            }
            else if (input.AccessKind == TaskAccessKind.OwnerOperated && !IsHttpsUrl(resource))
            {
                add("access_resource", "The resource of the owner must be an absolute HTTPS address.");
            }
        }

        if (input.Modes.HasFlag(TaskMode.Review))
        {
            var criteria = input.ReviewCriteria?.Trim() ?? string.Empty;

            if (criteria.Length is < 1 or > ReviewCriteriaMaxLength)
            {
                add("review_criteria", $"A review task names the structure of the assessment of 1 to {ReviewCriteriaMaxLength} characters.");
            }
        }
    }

    /// <summary>
    /// Checks the rules that only a published task has to hold. A published
    /// task holds the acceptance rules of its modes and names at least one hard
    /// requirement, so the marketplace can match a contributor against it and
    /// the eligibility of the task has an answer.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> ValidatePublication(TaskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var entry in ValidateAcceptance(input))
        {
            errors[entry.Key] = [.. entry.Value];
        }

        if (input.Requirements.Count == 0)
        {
            errors["requirements"] = ["A published task names at least one hard requirement."];
        }

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Checks the deadline of a task. The deadline lies between the day after
    /// the publication and the thirtieth day after it.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> ValidateDeadline(
        DateTimeOffset? deadline,
        DateTimeOffset now)
    {
        if (deadline is null)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["deadline"] = ["A task names the deadline of its work."],
            };
        }

        var earliest = now.AddHours(DeadlineMinHours);
        var latest = now.AddDays(DeadlineMaxDays);

        if (deadline < earliest)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["deadline"] = [$"The deadline is at least {DeadlineMinHours} hours away."],
            };
        }

        if (deadline > latest)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["deadline"] = [$"The deadline is at most {DeadlineMaxDays} days away."],
            };
        }

        return new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    /// <summary>True when the value is an absolute HTTPS address.</summary>
    public static bool IsHttpsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(uri.Host);
    }
}
