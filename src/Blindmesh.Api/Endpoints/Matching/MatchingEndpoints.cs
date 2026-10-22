using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Matching;

namespace Blindmesh.Api.Endpoints.Matching;

/// <summary>
/// The endpoints of the matching module. The list of the candidates belongs to
/// the members of the project of the task: every other account is refused
/// without a name and without a condition, so a refusal teaches nothing about
/// the task. The list is a read, so it writes nothing and it names no ledger
/// row.
/// </summary>
public static class MatchingEndpoints
{
    public static void MapMatchingEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/tasks/{id}/candidates", HandleGetCandidatesAsync)
            .WithName("ListTaskCandidates")
            .WithSummary("Read the accounts that fit a task of the project of the caller.")
            .Produces<CandidateListView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleGetCandidatesAsync(
        HttpContext context,
        MatchingService matchingService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.Request.RouteValues["id"] as string, out var taskId))
        {
            return NotFound(context);
        }

        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                MatchingErrorCodes.SessionInvalid,
                "The session of this account is no longer valid.");
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                MatchingErrorCodes.SessionRequired,
                "A signed in contributor is required to read the candidates of a task.");
        }

        var result = await matchingService.ListCandidatesAsync(
            session.UserId!.Value,
            taskId,
            cancellationToken);

        if (result.Status == MatchingCommandStatus.NotFound)
        {
            return NotFound(context);
        }

        if (result.Status == MatchingCommandStatus.Forbidden)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                MatchingErrorCodes.Forbidden,
                "Only the members of the project of the task may read its candidates.");
        }

        return Results.Ok(result.List);
    }

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            MatchingErrorCodes.NotFound,
            "The task does not exist or is not visible to you.");
}
