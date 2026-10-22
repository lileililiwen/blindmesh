namespace Blindmesh.Domain.Matching;

/// <summary>
/// The reason of the place of a candidate in the order of the list. The rule of
/// the exploration places an account without an accepted contribution before an
/// account with one. The reason names that rule in words; it is not a score,
/// not a weight, not a percentage and not a rank.
/// </summary>
public sealed record MatchOrderReason(string Code, string Text)
{
    /// <summary>The code of the reason of an account without an accepted contribution.</summary>
    public const string ExplorerCode = "explorer";

    /// <summary>The code of the reason of an account with an accepted contribution.</summary>
    public const string EstablishedCode = "established";

    /// <summary>
    /// The reason of an account. An account without an accepted contribution is
    /// an explorer, and the explorer comes first. An account the reputation
    /// module does not know has no accepted contribution, so it is an explorer
    /// too.
    /// </summary>
    public static MatchOrderReason Of(int acceptedContributions)
    {
        if (acceptedContributions <= 0)
        {
            return new MatchOrderReason(ExplorerCode, "No accepted contribution yet");
        }

        var noun = acceptedContributions == 1 ? "contribution" : "contributions";

        return new MatchOrderReason(EstablishedCode, $"{acceptedContributions} accepted {noun}");
    }
}
