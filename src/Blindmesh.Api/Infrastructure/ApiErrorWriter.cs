using Blindmesh.Application.Contracts;

namespace Blindmesh.Api.Infrastructure;

/// <summary>
/// Writes the single error envelope of the API. The envelope never contains SQL,
/// provider, stack trace or credential details.
/// </summary>
public static class ApiErrorWriter
{
    public const string BadRequest = "bad_request";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string PayloadTooLarge = "payload_too_large";
    public const string Unprocessable = "unprocessable_entity";
    public const string TooManyRequests = "too_many_requests";
    public const string ServiceUnavailable = "service_unavailable";
    public const string InternalError = "internal_error";
    public const string InvalidIdempotencyKey = "invalid_idempotency_key";
    public const string IdempotencyKeyReuse = "idempotency_key_reuse";

    public static IResult Create(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        var requestId = RequestIdMiddleware.GetRequestId(context);

        var body = new ApiErrorResponse(new ApiErrorBody
        {
            Code = code,
            Message = message,
            FieldErrors = fieldErrors,
            RequestId = requestId,
        });

        return Results.Json(body, statusCode: statusCode);
    }
}
