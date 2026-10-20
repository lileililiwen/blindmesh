namespace Blindmesh.Application.Identity;

/// <summary>
/// A change of the profile. A field that is left out keeps its current value,
/// so a caller never has to send the whole profile to change one field.
/// </summary>
public sealed class ProfilePatch
{
    public string? Username { get; init; }

    public string? DisplayName { get; init; }

    public string? Bio { get; init; }

    public string? Timezone { get; init; }

    public string[]? Skills { get; init; }

    public string[]? Languages { get; init; }

    public bool? IsPublic { get; init; }
}
