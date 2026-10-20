using System.Text;

namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// The rules of a capability. They live in the domain so the service, the API
/// and the interface agree on one definition of a valid capability, of an
/// allowed metadata key and of a matching condition.
/// </summary>
public static class CapabilityRules
{
    public const int LabelMaxLength = 100;

    public const int TagMaxLength = 50;

    public const int MaxTags = 20;

    public const int MaxMetadataEntries = 10;

    public const int MetadataKeyMaxLength = 40;

    public const int MetadataValueMaxLength = 100;

    public const int ConditionValueMaxLength = 100;

    public const int MaxConditions = 20;

    /// <summary>
    /// The keys of the metadata that a declaration may carry. Every key names a
    /// non sensitive identifier of the operating system, of the browser, of the
    /// device, of the region or of the provider. A key outside this list is
    /// refused, so a credential can never reach the database.
    /// </summary>
    public static IReadOnlyList<string> AllowedMetadataKeys { get; } =
    [
        "os",
        "os_version",
        "browser",
        "browser_version",
        "device",
        "device_model",
        "region",
        "provider",
    ];

    /// <summary>
    /// Checks every field of the editable content of a capability. The result
    /// holds one entry per invalid field, so the interface can attach the
    /// message to the field.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Validate(CapabilityInput input)
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

        var label = input.Label.Trim();

        if (label.Length is < 1 or > LabelMaxLength)
        {
            Add("label", $"The label must be 1 to {LabelMaxLength} characters long.");
        }
        else if (HasControlCharacter(label))
        {
            Add("label", "The label must not hold control characters.");
        }

        if (input.Tags.Count > MaxTags)
        {
            Add("tags", $"A capability holds at most {MaxTags} tags.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < input.Tags.Count; index++)
        {
            var tag = input.Tags[index].Trim();

            if (tag.Length is < 1 or > TagMaxLength)
            {
                Add("tags", $"Every tag must be 1 to {TagMaxLength} characters long.");
            }
            else if (!IsAllowedTag(tag))
            {
                Add("tags", "A tag holds only letters, digits, spaces, dots, dashes and underscores.");
            }
            else if (!seen.Add(Normalize(tag)))
            {
                Add("tags", "A tag must not be repeated.");
            }
        }

        if (input.Metadata.Count > MaxMetadataEntries)
        {
            Add("metadata", $"A capability holds at most {MaxMetadataEntries} metadata entries.");
        }

        foreach (var entry in input.Metadata)
        {
            if (!IsAllowedMetadataKey(entry.Key))
            {
                // The refused key is never echoed back. The message only names
                // the keys that are allowed.
                Add("metadata", "The metadata holds a key that is not allowed. Allowed keys are: " +
                    string.Join(", ", AllowedMetadataKeys) + ".");
            }
            else if (entry.Value is null || !IsAllowedMetadataValue(entry.Value))
            {
                Add("metadata",
                    $"The value of a metadata key holds only letters, digits, dots, dashes and underscores " +
                    $"and is at most {MetadataValueMaxLength} characters long.");
            }
        }

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Checks a set of hard conditions. The result holds one entry per invalid
    /// condition, so the caller can attach the message to the condition.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> ValidateConditions(
        IReadOnlyList<CapabilityHardCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

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

        if (conditions.Count == 0)
        {
            Add("conditions", "At least one condition is required.");
        }

        if (conditions.Count > MaxConditions)
        {
            Add("conditions", $"At most {MaxConditions} conditions are evaluated at once.");
        }

        for (var index = 0; index < conditions.Count; index++)
        {
            var condition = conditions[index];
            var field = $"conditions[{index}]";

            if (!CapabilityHardCondition.MatchableKinds.Contains(condition.Kind))
            {
                Add(field, "A condition uses the kind region, device or role.");
            }

            var value = condition.Value.Trim();

            if (value.Length is < 1 or > ConditionValueMaxLength)
            {
                Add(field, $"The value of a condition must be 1 to {ConditionValueMaxLength} characters long.");
            }
            else if (HasControlCharacter(value))
            {
                Add(field, "The value of a condition must not hold control characters.");
            }
        }

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>True when the key is one of the allowed metadata keys.</summary>
    public static bool IsAllowedMetadataKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MetadataKeyMaxLength)
        {
            return false;
        }

        return AllowedMetadataKeys.Contains(key.Trim().ToLowerInvariant());
    }

    /// <summary>True when the value of a metadata key is a plain identifier.</summary>
    public static bool IsAllowedMetadataValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MetadataValueMaxLength)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The form of a value that a match compares. The value is trimmed, its
    /// inner spaces are collapsed and it is case folded, so the match does not
    /// depend on the spelling of the contributor.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>True when the label or one of the tags of a declaration matches the value.</summary>
    public static bool Matches(string label, IReadOnlyList<string> tags, string value)
    {
        var wanted = Normalize(value);

        if (wanted.Length == 0)
        {
            return false;
        }

        if (Normalize(label) == wanted)
        {
            return true;
        }

        foreach (var tag in tags)
        {
            if (Normalize(tag) == wanted)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllowedTag(string tag)
    {
        foreach (var character in tag)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is ' ' or '.' or '-' or '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
