using System.Text.Json;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Notifications;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Notifications;

/// <summary>
/// The notifications of the signed in contributor. The list holds the
/// notifications of the caller alone, the read of one notification of another
/// account is refused without naming the summary of it, and the preferences
/// belong to the caller alone. The endpoints never write a ledger row.
/// </summary>
public static class NotificationEndpoints
{
    private static readonly string[] PreferenceFields = ["in_app_enabled", "email_enabled"];

    public static void MapNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/notifications", HandleListAsync)
            .WithName("ListNotifications")
            .WithSummary("List the notifications of the signed in contributor, the newest first.")
            .Produces<PagedResult<NotificationView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/notifications/{id}/read", HandleMarkReadAsync)
            .WithName("MarkNotificationRead")
            .WithSummary("Mark one notification of the signed in contributor as read.")
            .Produces<NotificationView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/notification-preferences", HandleReadPreferencesAsync)
            .WithName("ReadNotificationPreferences")
            .WithSummary("Read the notification preferences of the signed in contributor.")
            .Produces<NotificationPreferenceView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);

        app.MapPatch("/v1/notification-preferences", HandleUpdatePreferencesAsync)
            .WithName("UpdateNotificationPreferences")
            .WithSummary("Save the notification preferences of the signed in contributor.")
            .Accepts<UpdateNotificationPreferencesRequest>("application/json")
            .Produces<NotificationPreferenceView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> HandleListAsync(
        HttpContext context,
        NotificationService notifications,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                NotificationErrorCodes.SessionRequired,
                "The session of this account is no longer valid.");
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                NotificationErrorCodes.SessionRequired,
                "A signed in contributor is required to read the own notifications.");
        }

        var rawCursor = context.Request.Query["cursor"].ToString();
        NotificationCursor? cursor = null;

        if (!string.IsNullOrWhiteSpace(rawCursor))
        {
            cursor = NotificationCursor.Decode(rawCursor);

            if (cursor is null)
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    NotificationErrorCodes.BadRequest,
                    "The cursor of the list is not valid.");
            }
        }

        var limit = ReadLimit(context.Request.Query["limit"].ToString());
        var page = await notifications.ListAsync(session.UserId!.Value, cursor, limit, cancellationToken);

        return Results.Ok(page);
    }

    private static async Task<IResult> HandleMarkReadAsync(
        HttpContext context,
        NotificationService notifications,
        IdentitySessionGuard guard,
        Guid id,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                NotificationErrorCodes.SessionRequired,
                "The session of this account is no longer valid.");
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                NotificationErrorCodes.SessionRequired,
                "A signed in contributor is required to read a notification.");
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                NotificationErrorCodes.Forbidden,
                "The request does not carry the CSRF token of the session.");
        }

        var result = await notifications.MarkReadAsync(session.UserId!.Value, id, cancellationToken);

        if (result.Status == NotificationReadStatus.NotFound)
        {
            // The answer names neither the summary of the notification nor the
            // identity of the caller.
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status404NotFound,
                NotificationErrorCodes.NotFound,
                "The notification does not exist.");
        }

        return Results.Ok(result.Notification);
    }

    private static async Task<IResult> HandleReadPreferencesAsync(
        HttpContext context,
        NotificationService notifications,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                NotificationErrorCodes.SessionRequired,
                "The session of this account is no longer valid.");
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                NotificationErrorCodes.SessionRequired,
                "A signed in contributor is required to read the own notification preferences.");
        }

        var preferences = await notifications.GetPreferencesAsync(session.UserId!.Value, cancellationToken);

        return Results.Ok(preferences);
    }

    private static async Task<IResult> HandleUpdatePreferencesAsync(
        HttpContext context,
        NotificationService notifications,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                NotificationErrorCodes.SessionRequired,
                "The session of this account is no longer valid.");
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                NotificationErrorCodes.SessionRequired,
                "A signed in contributor is required to change the own notification preferences.");
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                NotificationErrorCodes.Forbidden,
                "The request does not carry the CSRF token of the session.");
        }

        var body = await ApiRequestReader.ReadAsync<UpdateNotificationPreferencesRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            PreferenceFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                NotificationErrorCodes.BadRequest,
                "The request body is not valid. It holds only the fields in_app_enabled and email_enabled.");
        }

        var result = await notifications.UpdatePreferencesAsync(
            session.UserId!.Value,
            new NotificationPreferenceInput(
                body.Value.InAppEnabled,
                body.Value.EmailEnabled),
            cancellationToken);

        return Results.Ok(result.Preferences);
    }

    /// <summary>The size of the page of the list. A value that is not a number
    /// falls back to the default of the product.</summary>
    private static int? ReadLimit(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw, out var limit) ? limit : null;
    }
}
