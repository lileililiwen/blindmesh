using System.Text;
using System.Text.Json;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Identity;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Identity;

/// <summary>
/// Reads the small request bodies of the identity module. A body that is not
/// valid JSON is refused with the standard error envelope; the parser never
/// echoes the body back to the caller.
/// </summary>
internal static class IdentityRequestReader
{
    public static async Task<T?> ReadAsync<T>(
        HttpRequest request,
        JsonSerializerOptions serializerOptions,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        string raw;

        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
        {
            raw = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(raw, serializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static JsonSerializerOptions SerializerOptions(IOptions<JsonOptions> jsonOptions) =>
        jsonOptions.Value.SerializerOptions;
}

/// <summary>The body of the sign in start command.</summary>
public sealed record StartSignInRequest(string? ReturnPath);

/// <summary>
/// The body of the profile change. The expected version protects the profile
/// against a lost change and the user id protects it against a change of
/// another account.
/// </summary>
public sealed record UpdateProfileRequest(
    int? ExpectedVersion,
    Guid? UserId,
    string? Username,
    string? DisplayName,
    string? Bio,
    string? Timezone,
    string[]? Skills,
    string[]? Languages,
    bool? IsPublic);
