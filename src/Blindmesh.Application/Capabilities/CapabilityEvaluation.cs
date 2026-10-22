using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The evaluation of a hard condition against the matchable declarations of an
/// account. The helper lives in the application assembly, so the eligibility
/// of the own contributor and the evaluation of another account never drift
/// apart. The rules, the vocabulary of the kinds and the privacy of a
/// declaration are the same in both.
/// </summary>
internal static class CapabilityEvaluation
{
    /// <summary>
    /// The outcome of a condition for the owner of the declarations. The
    /// reason speaks to that owner.
    /// </summary>
    internal static CapabilityConditionOutcome EvaluateForOwner(
        CapabilityHardCondition condition,
        IReadOnlyList<MatchableCapability> declarations)
    {
        var kind = CapabilityService.KindText(condition.Kind);
        var value = condition.Value.Trim();
        var match = Match(condition, declarations, value);

        if (match is not null)
        {
            return new CapabilityConditionOutcome(
                kind,
                value,
                true,
                $"Your public {kind} declaration '{match.Capability.Label}' matches the required {kind} '{value}'.");
        }

        return new CapabilityConditionOutcome(
            kind,
            value,
            false,
            $"You have no public and available {kind} declaration that matches '{value}'. " +
            $"Declare one, or make your {kind} declaration public and available, so it can be matched.");
    }

    /// <summary>
    /// The outcome of a condition for another account. The reason names that
    /// account instead of saying "your", and it names the public declaration
    /// that satisfied the condition with its label and the tag that matched.
    /// </summary>
    internal static CapabilityConditionOutcome EvaluateForCandidate(
        CapabilityHardCondition condition,
        IReadOnlyList<MatchableCapability> declarations)
    {
        var kind = CapabilityService.KindText(condition.Kind);
        var value = condition.Value.Trim();
        var match = Match(condition, declarations, value);

        if (match is not null)
        {
            return new CapabilityConditionOutcome(
                kind,
                value,
                true,
                $"The public {kind} declaration '{match.Capability.Label}' of the account " +
                $"matches the required {kind} '{value}'.",
                match.Capability.Label,
                match.MatchingTag);
        }

        return new CapabilityConditionOutcome(
            kind,
            value,
            false,
            $"The account has no public and available {kind} declaration that matches '{value}'.");
    }

    /// <summary>
    /// The declaration that satisfies the condition, with the tag that matched.
    /// The tag is null when the label of the declaration matched itself.
    /// </summary>
    private static DeclarationMatch? Match(
        CapabilityHardCondition condition,
        IReadOnlyList<MatchableCapability> declarations,
        string value)
    {
        foreach (var declaration in declarations)
        {
            if (declaration.Capability.Kind != condition.Kind)
            {
                continue;
            }

            if (!CapabilityRules.Matches(declaration.Capability.Label, declaration.Tags, value))
            {
                continue;
            }

            return new DeclarationMatch(declaration.Capability, MatchingTag(declaration, value));
        }

        return null;
    }

    /// <summary>
    /// The tag of the declaration that matches the value of the condition. Null
    /// when the label of the declaration matched the value itself.
    /// </summary>
    private static string? MatchingTag(MatchableCapability declaration, string value)
    {
        if (CapabilityRules.Normalize(declaration.Capability.Label) == CapabilityRules.Normalize(value))
        {
            return null;
        }

        foreach (var tag in declaration.Tags)
        {
            if (CapabilityRules.Normalize(tag) == CapabilityRules.Normalize(value))
            {
                return tag;
            }
        }

        return null;
    }

    /// <summary>One declaration that satisfied a condition, with its matching tag.</summary>
    private sealed record DeclarationMatch(Capability Capability, string? MatchingTag);
}
