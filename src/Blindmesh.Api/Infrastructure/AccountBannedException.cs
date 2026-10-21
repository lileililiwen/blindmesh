namespace Blindmesh.Api.Infrastructure;

/// <summary>
/// The refusal of a banned account. The session guard throws it before it hands
/// out the actor of a request, so a banned account never reaches a domain or a
/// monetary effect. The error envelope of the refusal carries the code
/// <c>account_banned</c>.
/// </summary>
public sealed class AccountBannedException : Exception
{
    public AccountBannedException()
        : base("The account of the request is banned.")
    {
    }
}
