namespace Blindmesh.Domain.Identity;

/// <summary>The editable profile of a contributor.</summary>
public sealed class ProfileInput
{
    public string Username { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Bio { get; init; } = string.Empty;

    public string Timezone { get; init; } = "UTC";

    public string[] Skills { get; init; } = [];

    public string[] Languages { get; init; } = [];

    public bool IsPublic { get; init; } = true;
}
