namespace Blindmesh.Application.Runtime;

/// <summary>
/// Rules for the Idempotency-Key header required by money, publication,
/// application, submission, review and moderation commands.
/// </summary>
public static class IdempotencyKey
{
    public const int MinLength = 16;
    public const int MaxLength = 128;

    /// <summary>
    /// A key is valid when it is 16 to 128 printable ASCII characters
    /// (0x21 to 0x7E). Anything else is rejected before any side effect.
    /// </summary>
    public static bool IsValid(string? key)
    {
        if (key is null || key.Length is < MinLength or > MaxLength)
        {
            return false;
        }

        foreach (var character in key)
        {
            if (character is < (char)0x21 or > (char)0x7E)
            {
                return false;
            }
        }

        return true;
    }
}
