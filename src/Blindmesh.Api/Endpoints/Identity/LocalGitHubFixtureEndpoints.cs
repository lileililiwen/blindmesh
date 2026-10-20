using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Identity;
using Blindmesh.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Identity;

/// <summary>
/// The local GitHub fixture. It serves the seeded accounts of the product so
/// the sign in journey can be exercised without a provider account. The
/// fixture is only reachable with APP_ENV=test; every other host answers the
/// safe not found.
/// </summary>
public static class LocalGitHubFixtureEndpoints
{
    public static void MapLocalGitHubFixtureEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/test/github/authorize", HandleAuthorizeAsync)
            .WithName("LocalGitHubFixtureAuthorize")
            .WithSummary("Authorize a seeded account of the local GitHub fixture.")
            .ExcludeFromDescription()
            .Produces(StatusCodes.Status302Found)
            .Produces<Application.Contracts.ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<Application.Contracts.ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAuthorizeAsync(
        HttpContext context,
        LocalGitHubFixtureGate gate,
        IOptions<IdentityOptions> identityOptions,
        CancellationToken cancellationToken)
    {
        if (!gate.IsEnabled)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status404NotFound,
                IdentityErrorCodes.NotFound,
                "The page does not exist.");
        }

        var state = context.Request.Query["state"].ToString();

        if (string.IsNullOrWhiteSpace(state))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ApiErrorWriter.BadRequest,
                "The request does not carry a state.");
        }

        var actor = LocalGitHubFixture.Find(context.Request.Query["actor"].ToString())
            ?? LocalGitHubFixture.Actors[0];

        // The callback address comes from the configuration of the host and
        // never from the request, so the fixture cannot redirect a visitor
        // to another host.
        var callback = identityOptions.Value.CallbackUrl;
        var location = $"{callback}?code={Uri.EscapeDataString(actor.Key)}&state={Uri.EscapeDataString(state)}";

        return Results.Redirect(location);
    }
}
