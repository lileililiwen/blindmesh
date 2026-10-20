namespace Blindmesh.Domain.Projects;

/// <summary>
/// The rules of a project. They live in the domain so the service, the API and
/// the interface agree on one definition of a valid project and of an allowed
/// transition.
/// </summary>
public static class ProjectRules
{
    public const int NameMaxLength = 100;

    public const int SummaryMaxLength = 500;

    public const int DescriptionMaxLength = 10000;

    public const int RepositoryUrlMaxLength = 2048;

    public const int VersionLabelMaxLength = 100;

    public const int CommitReferenceMaxLength = 128;

    /// <summary>
    /// Checks every field of the editable content of a project. The result
    /// holds one entry per invalid field, so the interface can attach the
    /// message to the field.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Validate(ProjectInput input)
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

        var name = input.Name.Trim();

        if (name.Length is < 1 or > NameMaxLength)
        {
            Add("name", $"The name must be 1 to {NameMaxLength} characters long.");
        }

        if (input.Summary.Trim().Length > SummaryMaxLength)
        {
            Add("summary", $"The summary must be at most {SummaryMaxLength} characters long.");
        }

        if (input.Description.Trim().Length > DescriptionMaxLength)
        {
            Add("description", $"The description must be at most {DescriptionMaxLength} characters long.");
        }

        var repositoryUrl = input.RepositoryUrl.Trim();

        if (repositoryUrl.Length is < 1 or > RepositoryUrlMaxLength)
        {
            Add("repository_url", $"The repository address must be 1 to {RepositoryUrlMaxLength} characters long.");
        }
        else if (!IsHttpsUrl(repositoryUrl))
        {
            Add("repository_url", "The repository address must be an absolute HTTPS address.");
        }

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Checks the label of a version and its optional commit reference.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> ValidateVersion(string? label, string? commitReference)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var trimmedLabel = label?.Trim() ?? string.Empty;

        if (trimmedLabel.Length is < 1 or > VersionLabelMaxLength)
        {
            errors["label"] = [$"The version label must be 1 to {VersionLabelMaxLength} characters long."];
        }

        var trimmedReference = commitReference?.Trim() ?? string.Empty;

        if (trimmedReference.Length > CommitReferenceMaxLength)
        {
            errors["commit_reference"] = [$"The commit reference must be at most {CommitReferenceMaxLength} characters long."];
        }

        return errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The allowed transitions of the lifecycle of a project: Draft to Active,
    /// Active to Paused, Paused to Active and every state that is not archived
    /// to Archived. An archived project is final.
    /// </summary>
    public static bool IsAllowedTransition(ProjectStatus from, ProjectStatus to) =>
        (from, to) switch
        {
            (ProjectStatus.Draft, ProjectStatus.Active) => true,
            (ProjectStatus.Active, ProjectStatus.Paused) => true,
            (ProjectStatus.Paused, ProjectStatus.Active) => true,
            (ProjectStatus.Draft, ProjectStatus.Archived) => true,
            (ProjectStatus.Active, ProjectStatus.Archived) => true,
            (ProjectStatus.Paused, ProjectStatus.Archived) => true,
            _ => false,
        };

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
