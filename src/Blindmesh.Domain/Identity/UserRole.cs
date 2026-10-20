namespace Blindmesh.Domain.Identity;

/// <summary>
/// The roles of the product. Developer, tester and resource provider are
/// concurrent activities, not exclusive roles, so they are not roles here.
/// </summary>
public static class UserRole
{
    public const string User = "user";

    public const string Moderator = "moderator";

    public const string Administrator = "administrator";

    public static bool IsKnown(string? role) =>
        role is User or Moderator or Administrator;
}
