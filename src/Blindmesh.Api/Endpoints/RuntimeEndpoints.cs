using System.Text;
using System.Text.Json;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Runtime;
using Blindmesh.Domain.Shared;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints;

/// <summary>
/// The runtime command of the foundation. It exercises the idempotency, the
/// transaction and the outbox seams of the product on a real command that has no
/// business meaning beyond proving the shell works.
/// </summary>
public static class RuntimeEndpoints
{
    public const string IdempotencyHeaderName = "Idempotency-Key";

    public static void MapRuntimeEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/runtime/heartbeat", HandleHeartbeatAsync)
            .WithName("RuntimeHeartbeat")
            .WithSummary("Record a runtime heartbeat and publish an outbox event.")
            .Produces<RuntimeHeartbeatResult>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleHeartbeatAsync(
        HttpContext context,
        RuntimeHeartbeatService heartbeatService,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<JsonOptions> jsonOptions,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var key = context.Request.Headers[IdempotencyHeaderName].ToString();

        if (!IdempotencyKey.IsValid(key))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ApiErrorWriter.InvalidIdempotencyKey,
                $"The {IdempotencyHeaderName} header is required and must be 16 to 128 printable ASCII characters.",
                new Dictionary<string, string[]>
                {
                    [IdempotencyHeaderName] = ["Use 16 to 128 printable ASCII characters."],
                });
        }

        var rawBody = await ReadBodyAsync(context.Request, cancellationToken);
        string? note;

        try
        {
            note = ReadNote(rawBody);
        }
        catch (JsonException)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ApiErrorWriter.BadRequest,
                "The request body is not valid JSON.");
        }

        var payloadHash = PayloadHasher.Compute(rawBody);
        var idempotencyScope = $"anonymous|{OutboxEventTypes.RuntimeHeartbeat}";

        var existing = await idempotencyStore.FindAsync(idempotencyScope, key, cancellationToken);

        if (existing is not null)
        {
            return ReplayOrReject(context, existing, payloadHash);
        }

        ICommandTransactionScope? transaction = await transactionFactory.BeginAsync(cancellationToken);

        try
        {
            var commandResult = await heartbeatService.RecordAsync(null, note, cancellationToken);
            var responseBody = JsonSerializer.Serialize(commandResult, jsonOptions.Value.SerializerOptions);

            await idempotencyStore.SaveAsync(
                new IdempotencyRecord
                {
                    Scope = idempotencyScope,
                    Key = key,
                    PayloadHash = payloadHash,
                    StatusCode = StatusCodes.Status201Created,
                    ResponseBody = responseBody,
                    CreatedAt = clock.UtcNow,
                    ExpiresAt = clock.UtcNow.AddDays(7),
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Results.Text(responseBody, "application/json", statusCode: StatusCodes.Status201Created);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "ux_idempotency_records_scope_key"))
        {
            // A concurrent request stored the same key first. Its committed
            // response is the only truthful answer for this key.
            await transaction.DisposeAsync();
            transaction = null;

            using var scope = scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            var stored = await store.FindAsync(idempotencyScope, key, cancellationToken);

            if (stored is null)
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status409Conflict,
                    ApiErrorWriter.Conflict,
                    "The command is already running with this key.");
            }

            return ReplayOrReject(context, stored, payloadHash);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static IResult ReplayOrReject(HttpContext context, IdempotencyRecord record, string payloadHash)
    {
        if (!string.Equals(record.PayloadHash, payloadHash, StringComparison.Ordinal))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status409Conflict,
                ApiErrorWriter.IdempotencyKeyReuse,
                "The idempotency key was already used with a different payload.");
        }

        return Results.Text(record.ResponseBody, "application/json", statusCode: record.StatusCode);
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string? ReadNote(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        using var document = JsonDocument.Parse(rawBody);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return document.RootElement.TryGetProperty("note", out var note) && note.ValueKind == JsonValueKind.String
            ? note.GetString()
            : null;
    }
}
