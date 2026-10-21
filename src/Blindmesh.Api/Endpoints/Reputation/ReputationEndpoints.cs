using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Reputation;

namespace Blindmesh.Api.Endpoints.Reputation;

/// <summary>
/// The reputation of the contributors. The public answers hold the counts and
/// the sources that are public right now; the own answer holds the history of
/// the signed in contributor. The endpoints never write a ledger row.
/// </summary>
public static class ReputationEndpoints
{
    public static void MapReputationEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/people/{username}/reputation", HandlePublicSummaryAsync)
            .WithName("ReadPublicReputation")
            .WithSummary("Read the public reputation summary of a contributor.")
            .Produces<ReputationSummaryView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/people/{username}/reputation/history", HandlePublicHistoryAsync)
            .WithName("ReadPublicReputationHistory")
            .WithSummary("Read the public history of the reputation of a contributor.")
            .Produces<ReputationHistoryView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/reputation/history", HandleOwnHistoryAsync)
            .WithName("ReadOwnReputationHistory")
            .WithSummary("Read the own history of the reputation of the signed in contributor.")
            .Produces<ReputationHistoryView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandlePublicSummaryAsync(
        HttpContext context,
        ReputationService reputation,
        IdentitySessionGuard guard,
        string username,
        CancellationToken cancellationToken)
    {
        if (await IsBannedAsync(context, guard, cancellationToken))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                ReputationErrorCodes.SessionRequired,
                "The session of this account is no longer valid.");
        }

        var summary = await reputation.GetPublicAsync(username, cancellationToken);

        if (summary is null)
        {
            return NotFound(context);
        }

        return Results.Ok(summary);
    }

    private static async Task<IResult> HandlePublicHistoryAsync(
        HttpContext context,
        ReputationService reputation,
        IdentitySessionGuard guard,
        string username,
        CancellationToken cancellationToken)
    {
        if (await IsBannedAsync(context, guard, cancellationToken))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                ReputationErrorCodes.SessionRequired,
                "The session of this account is no longer valid.");
        }

        var history = await reputation.GetPublicHistoryAsync(username, cancellationToken);

        if (history is null)
        {
            return NotFound(context);
        }

        return Results.Ok(history);
    }

    private static async Task<IResult> HandleOwnHistoryAsync(
        HttpContext context,
        ReputationService reputation,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                ReputationErrorCodes.SessionRequired,
                "The session of this account is no longer valid.");
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                ReputationErrorCodes.SessionRequired,
                "A session is required to read the own reputation.");
        }

        var history = await reputation.GetOwnHistoryAsync(session.UserId!.Value, cancellationToken);

        if (history is null)
        {
            return NotFound(context);
        }

        return Results.Ok(history);
    }

    private static async Task<bool> IsBannedAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        return session is { IsBanned: true };
    }

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            ReputationErrorCodes.NotFound,
            "The contributor does not exist.");
}
