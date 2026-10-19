namespace Blindmesh.Application.Contracts;

/// <summary>
/// The single error envelope used by every API response:
/// { "error": { "code", "message", "field_errors"?, "request_id" } }.
/// Messages never contain SQL, provider or credential details.
/// </summary>
public sealed record ApiErrorResponse(ApiErrorBody Error);

public sealed record ApiErrorBody
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; init; }

    public required string RequestId { get; init; }
}
