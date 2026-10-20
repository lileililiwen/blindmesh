using System.Globalization;
using System.Text.Json;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Credits;
using Blindmesh.Application.Runtime;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Credits;

/// <summary>
/// The credit endpoints of the product. The wallet of a contributor is private:
/// a wallet of another contributor is not found and never carries an amount.
/// There is no public transfer and no public balance editor; the escrow
/// primitives are internal and belong to the package that owns the task.
/// </summary>
public static class CreditEndpoints
{
    public const string IdempotencyHeaderName = "Idempotency-Key";

    private const int DefaultLimit = 20;

    private const int MaxLimit = 100;

    private const int MaxAttempts = 3;

    private static readonly string[] GrantFields = ["rules_accepted"];

    public static void MapCreditEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/wallet", HandleGetWalletAsync)
            .WithName("GetWallet")
            .WithSummary("Read the wallet of the signed in contributor.")
            .Produces<CreditWalletView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/wallet/transactions", HandleListTransactionsAsync)
            .WithName("ListWalletTransactions")
            .WithSummary("List the transactions of the wallet of the signed in contributor.")
            .Produces<CreditTransactionPage>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPost("/v1/wallet/grant", HandleGrantAsync)
            .WithName("ClaimWalletGrant")
            .WithSummary("Claim the limited grant of the signed in contributor.")
            .Accepts<GrantRequest>("application/json")
            .Produces<CreditWalletView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ApiErrorResponse>(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> HandleGetWalletAsync(
        HttpContext context,
        ICreditService creditService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadOwnerId(context, actor!.UserId!.Value, out var ownerId, out var invalid))
        {
            return invalid!;
        }

        // The read materialises the expiry of the lots that ran out, so the
        // balance of the wallet and the balance of the journal are the same.
        await using var transaction = await transactionFactory.BeginAsync(cancellationToken);

        var wallet = await creditService.GetWalletAsync(actor, ownerId, cancellationToken);

        if (wallet is null)
        {
            return NotFound(context);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(wallet);
    }

    private static async Task<IResult> HandleListTransactionsAsync(
        HttpContext context,
        ICreditService creditService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadOwnerId(context, actor!.UserId!.Value, out var ownerId, out var invalid))
        {
            return invalid!;
        }

        var rawCursor = context.Request.Query["cursor"].ToString();
        CreditCursor? cursor = null;

        if (!string.IsNullOrWhiteSpace(rawCursor))
        {
            cursor = CreditCursor.Decode(rawCursor);

            if (cursor is null)
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    CreditErrorCodes.BadRequest,
                    "The cursor of the list is not valid.");
            }
        }

        var limit = ReadLimit(context.Request.Query["limit"].ToString());
        var page = await creditService.ListTransactionsAsync(actor, ownerId, limit, cursor, cancellationToken);

        if (page is null)
        {
            return NotFound(context);
        }

        return Results.Ok(page);
    }

    private static async Task<IResult> HandleGrantAsync(
        HttpContext context,
        ICreditService creditService,
        IdentitySessionGuard guard,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<JsonOptions> jsonOptions,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await ApiRequestReader.ReadAsync<GrantRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            GrantFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                CreditErrorCodes.BadRequest,
                "The request body is not valid. It holds only the field rules_accepted.");
        }

        var key = context.Request.Headers[IdempotencyHeaderName].ToString();

        if (!IdempotencyKey.IsValid(key))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ApiErrorWriter.InvalidIdempotencyKey,
                $"The {IdempotencyHeaderName} header must be 16 to 128 printable ASCII characters.",
                new Dictionary<string, string[]>
                {
                    [IdempotencyHeaderName] = ["Use 16 to 128 printable ASCII characters."],
                });
        }

        var payloadHash = PayloadHasher.Compute(body.Raw);
        var idempotencyScope = $"{actor!.UserId}|credit.grant";
        var existing = await idempotencyStore.FindAsync(idempotencyScope, key, cancellationToken);

        if (existing is not null)
        {
            return ReplayOrReject(context, existing, payloadHash);
        }

        var command = new CreditGrantCommand(
            body.Value.RulesAccepted,
            key,
            context.Connection.RemoteIpAddress?.ToString());

        for (var attempt = 1; ; attempt++)
        {
            ICommandTransactionScope? transaction = null;

            try
            {
                transaction = await transactionFactory.BeginAsync(cancellationToken);

                var result = await creditService.GrantOnceAsync(actor, command, cancellationToken);

                if (result.Status != CreditCommandStatus.Succeeded)
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                        transaction = null;
                    }

                    return ToResult(context, result);
                }

                var responseBody = JsonSerializer.Serialize(result.Wallet, jsonOptions.Value.SerializerOptions);

                await idempotencyStore.SaveAsync(
                    new IdempotencyRecord
                    {
                        Scope = idempotencyScope,
                        Key = key,
                        PayloadHash = payloadHash,
                        StatusCode = StatusCodes.Status200OK,
                        ResponseBody = responseBody,
                        CreatedAt = clock.UtcNow,
                        ExpiresAt = clock.UtcNow.AddDays(7),
                    },
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return Results.Text(responseBody, "application/json", statusCode: StatusCodes.Status200OK);
            }
            catch (DbUpdateException exception) when (
                PostgresErrors.IsUniqueViolation(exception, "ux_idempotency_records_scope_key"))
            {
                // A concurrent request stored the same key first. Its committed
                // response is the only truthful answer for this key.
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                    transaction = null;
                }

                using var commandScope = scopeFactory.CreateScope();
                var store = commandScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
                var stored = await store.FindAsync(idempotencyScope, key, cancellationToken);

                if (stored is null)
                {
                    return ApiErrorWriter.Create(
                        context,
                        StatusCodes.Status409Conflict,
                        CreditErrorCodes.Conflict,
                        "The command is already running with this key.");
                }

                return ReplayOrReject(context, stored, payloadHash);
            }
            catch (Exception exception) when (
                attempt < MaxAttempts && PostgresErrors.IsRetryableConflict(exception))
            {
                // A serialization failure or a deadlock of the database. The
                // command runs again with the same key and the same payload.
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                    transaction = null;
                }
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
    }

    private static async Task<(CreditActor? Actor, IResult? Refusal)> ReadSignedInActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                CreditErrorCodes.SessionInvalid,
                "The session of this account is no longer valid."));
        }

        if (session is null || !session.IsAuthenticated)
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                CreditErrorCodes.SessionRequired,
                "A signed in contributor is required for this request."));
        }

        return (new CreditActor(session.UserId, session.Username), null);
    }

    private static async Task<(CreditActor? Actor, IResult? Refusal)> ReadWriteActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return (null, refusal);
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                CreditErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session."));
        }

        return (actor, null);
    }

    /// <summary>
    /// The owner of the wallet of the request. It is the signed in contributor.
    /// A request for the wallet of another contributor is answered with the
    /// standard not found envelope, so the answer never carries an amount of
    /// a wallet the caller may not see.
    /// </summary>
    private static bool TryReadOwnerId(
        HttpContext context,
        Guid sessionUserId,
        out Guid ownerId,
        out IResult? invalid)
    {
        var raw = context.Request.Query["user_id"].ToString();

        if (string.IsNullOrWhiteSpace(raw))
        {
            ownerId = sessionUserId;
            invalid = null;
            return true;
        }

        if (!Guid.TryParse(raw, out ownerId))
        {
            invalid = ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                CreditErrorCodes.BadRequest,
                "The user_id of the request is not valid.");
            return false;
        }

        invalid = null;
        return true;
    }

    private static IResult ToResult(HttpContext context, CreditCommandResult result) => result.Status switch
    {
        CreditCommandStatus.Succeeded => Results.Ok(result.Wallet),
        CreditCommandStatus.NotFound => NotFound(context),
        CreditCommandStatus.Forbidden => ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            CreditErrorCodes.GrantNotEligible,
            result.Message ?? "The grant is not available for this account."),
        CreditCommandStatus.ValidationFailed => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            CreditErrorCodes.Unprocessable,
            "Some fields of the request are not valid.",
            result.FieldErrors),
        CreditCommandStatus.Conflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            result.Reasons?.Contains(CreditReasons.AlreadyClaimed) == true
                ? CreditErrorCodes.GrantAlreadyClaimed
                : CreditErrorCodes.Conflict,
            result.Message ?? "The state of the credits does not accept this command."),
        CreditCommandStatus.LimitReached => ApiErrorWriter.Create(
            context,
            StatusCodes.Status429TooManyRequests,
            CreditErrorCodes.GrantLimitReached,
            result.Message ?? "The grant of the day is used up. Try again tomorrow."),
        _ => ApiErrorWriter.Create(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorWriter.InternalError,
            "The grant could not be claimed."),
    };

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            CreditErrorCodes.NotFound,
            "The wallet does not exist.");

    private static int ReadLimit(string? raw)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit))
        {
            return DefaultLimit;
        }

        if (limit < 1)
        {
            return DefaultLimit;
        }

        return Math.Min(limit, MaxLimit);
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
}

/// <summary>The body of the grant command. It holds no credential.</summary>
public sealed class GrantRequest
{
    /// <summary>True when the contributor accepts the rules of the marketplace.</summary>
    public bool RulesAccepted { get; set; }
}
