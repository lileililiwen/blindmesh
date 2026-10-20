namespace Blindmesh.Domain.Credits;

/// <summary>
/// The rules of the credit ledger. They live in the domain, so the service,
/// the API, the interface and the tests decide the same way.
/// </summary>
public static class CreditRules
{
    /// <summary>
    /// The largest amount of credits a JSON value may carry. It stays inside
    /// the exact integer range of a browser.
    /// </summary>
    public const long MaxCredits = 9_000_000_000_000;

    /// <summary>The amount of the limited grant.</summary>
    public const long GrantAmount = 60;

    /// <summary>The number of days the granted credits stay valid.</summary>
    public const int GrantExpiryDays = 30;

    /// <summary>The number of days an account must be old to claim the grant.</summary>
    public const int GrantMinAccountAgeDays = 7;

    /// <summary>The number of credits the platform may issue in one day.</summary>
    public const long GrantDailyGlobalCap = 6000;

    /// <summary>The number of claims one address may make in one day.</summary>
    public const int GrantDailyAddressLimit = 5;

    /// <summary>The number of hours of the daily limits.</summary>
    public const int GrantWindowHours = 24;

    /// <summary>The longest reference of an escrow.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>The longest note of a transaction.</summary>
    public const int NoteMaxLength = 200;

    /// <summary>The reference of the platform accounts.</summary>
    public const string PlatformReference = "platform";

    /// <summary>The reference of the limited grant.</summary>
    public const string GrantReference = "grant";

    /// <summary>True when the amount may be moved by a credit command.</summary>
    public static bool IsValidAmount(long amount) => amount > 0 && amount <= MaxCredits;

    /// <summary>True when the reference of an escrow is usable.</summary>
    public static bool IsValidReference(string? reference) =>
        !string.IsNullOrWhiteSpace(reference) && reference.Trim().Length <= ReferenceMaxLength;
}
