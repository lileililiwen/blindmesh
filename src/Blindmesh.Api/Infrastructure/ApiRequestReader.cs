using System.Text;
using System.Text.Json;

namespace Blindmesh.Api.Infrastructure;

/// <summary>
/// Reads the small request bodies of the API. A body that is
/// not valid JSON is refused with the standard error envelope; the parser
/// never echoes the body back to the caller. A body that holds a field outside
/// the allowed ones is refused as well, so a credential that was sent in a
/// field of the body is never stored and never echoed.
/// </summary>
internal static class ApiRequestReader
{
    public static async Task<ApiBody<T>> ReadAsync<T>(
        HttpRequest request,
        JsonSerializerOptions serializerOptions,
        IReadOnlyCollection<string> allowedFields,
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
            return new ApiBody<T>(raw, default, false);
        }

        try
        {
            using var document = JsonDocument.Parse(raw);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ApiBody<T>(raw, default, false);
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!allowedFields.Contains(property.Name))
                {
                    // The refused field is never named back to the caller.
                    return new ApiBody<T>(raw, default, false);
                }
            }
        }
        catch (JsonException)
        {
            return new ApiBody<T>(raw, default, false);
        }

        try
        {
            return new ApiBody<T>(raw, JsonSerializer.Deserialize<T>(raw, serializerOptions), true);
        }
        catch (JsonException)
        {
            return new ApiBody<T>(raw, default, false);
        }
    }
}

/// <summary>The raw body of a request, its parsed value and its shape.</summary>
internal sealed record ApiBody<T>(string Raw, T? Value, bool IsValid);
