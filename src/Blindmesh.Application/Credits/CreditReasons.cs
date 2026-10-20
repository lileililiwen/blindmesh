using Blindmesh.Domain.Credits;

namespace Blindmesh.Application.Credits;

/// <summary>
/// The reasons the grant of a contributor is not available. The reasons are
/// part of the published contract, so the interface can explain the decision
/// without inventing a text of its own.
/// </summary>
public static class CreditReasons
{
    public const string EmailNotVerified =
        "Your provider account has no verified email address.";

    /// <summary>
    /// The account is too young. The text names the number of days of the
    /// rule, so it stays in step with the rule itself.
    /// </summary>
    public static readonly string AccountTooYoung =
        $"Your account must be at least {CreditRules.GrantMinAccountAgeDays} days old before you claim the grant.";

    public const string RulesNotAccepted =
        "You must accept the rules of the marketplace before you claim the grant.";

    public const string AlreadyClaimed =
        "You already claimed the grant of your provider account.";

    public const string DailyCapReached =
        "The grant of the day is used up. Try again tomorrow.";

    public const string AddressLimitReached =
        "Too many grants were claimed from this address today. Try again tomorrow.";
}
