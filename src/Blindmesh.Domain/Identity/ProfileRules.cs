namespace Blindmesh.Domain.Identity;

/// <summary>
/// The rules of the public contributor profile. They live in the domain so the
/// service, the API and the interface agree on one definition.
/// </summary>
public static class ProfileRules
{
    public const int UsernameMinLength = 3;

    public const int UsernameMaxLength = 30;

    public const int DisplayNameMinLength = 1;

    public const int DisplayNameMaxLength = 80;

    public const int BioMaxLength = 1000;

    public const int TagMaxLength = 40;

    public const int TagCountMax = 20;

    /// <summary>
    /// A username is stored case folded, so the unique index is case folded
    /// and a lookup of a different spelling finds the same profile.
    /// </summary>
    public static string NormalizeUsername(string value) =>
        value.Trim().ToLowerInvariant();

    /// <summary>
    /// Checks every field of the profile. The result holds one entry per
    /// invalid field, so the interface can attach the message to the field.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Validate(ProfileInput input)
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

        var username = input.Username.Trim();

        if (username.Length < UsernameMinLength || username.Length > UsernameMaxLength)
        {
            Add("username", $"The username must be {UsernameMinLength} to {UsernameMaxLength} characters long.");
        }
        else if (!IsValidUsername(username))
        {
            Add("username", "The username may only contain ASCII letters, digits and hyphens.");
        }

        var displayName = input.DisplayName.Trim();

        if (displayName.Length < DisplayNameMinLength || displayName.Length > DisplayNameMaxLength)
        {
            Add("display_name", $"The display name must be {DisplayNameMinLength} to {DisplayNameMaxLength} characters long.");
        }

        if (input.Bio.Length > BioMaxLength)
        {
            Add("bio", $"The bio must be at most {BioMaxLength} characters long.");
        }

        if (!IsValidTimezone(input.Timezone))
        {
            Add("timezone", "The time zone must be a valid IANA name, for example Europe/Berlin.");
        }

        ValidateTags(input.Skills, "skills", Add);
        ValidateTags(input.Languages, "languages", Add);

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>True when the value is a valid IANA time zone name.</summary>
    public static bool IsValidTimezone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>
    /// A username is 3 to 30 ASCII letters, digits and hyphens. It never
    /// starts or ends with a hyphen.
    /// </summary>
    public static bool IsValidUsername(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < UsernameMinLength || value.Length > UsernameMaxLength)
        {
            return false;
        }

        if (value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        foreach (var character in value)
        {
            var isAllowed = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '-';

            if (!isAllowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Derives a valid username from a provider login. The result is a
    /// suggestion; the contributor confirms it during the onboarding.
    /// </summary>
    public static string SuggestUsername(string login)
    {
        var builder = new System.Text.StringBuilder(login.Length);

        foreach (var character in login.Trim().ToLowerInvariant())
        {
            var isAllowed = character is >= 'a' and <= 'z' or >= '0' and <= '9';

            builder.Append(isAllowed ? character : '-');
        }

        var suggestion = builder.ToString().Trim('-');

        while (suggestion.Contains("--", StringComparison.Ordinal))
        {
            suggestion = suggestion.Replace("--", "-", StringComparison.Ordinal);
        }

        if (suggestion.Length < UsernameMinLength)
        {
            suggestion = $"user-{suggestion}".TrimEnd('-');
        }

        if (suggestion.Length > UsernameMaxLength)
        {
            suggestion = suggestion[..UsernameMaxLength].TrimEnd('-');
        }

        return suggestion;
    }

    private static void ValidateTags(string[]? tags, string field, Action<string, string> add)
    {
        if (tags is null)
        {
            add(field, $"At most {TagCountMax} tags are allowed.");
            return;
        }

        if (tags.Length > TagCountMax)
        {
            add(field, $"At most {TagCountMax} tags are allowed.");
        }

        foreach (var tag in tags)
        {
            var trimmed = tag.Trim();

            if (trimmed.Length == 0 || trimmed.Length > TagMaxLength)
            {
                add(field, $"Every tag must be 1 to {TagMaxLength} characters long.");
            }
        }
    }
}
