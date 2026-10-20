using System.Text;
using System.Text.Json;

namespace Blindmesh.Api.Endpoints.Projects;

/// <summary>
/// Reads the small request bodies of the projects module. A body that is not
/// valid JSON is refused with the standard error envelope; the parser never
/// echoes the body back to the caller. The raw body is returned as well, so
/// the create command can hash the exact bytes of the request.
/// </summary>
internal static class ProjectRequestReader
{
    public static async Task<ProjectBody<T>> ReadAsync<T>(
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
            return new ProjectBody<T>(raw, default);
        }

        try
        {
            return new ProjectBody<T>(raw, JsonSerializer.Deserialize<T>(raw, serializerOptions));
        }
        catch (JsonException)
        {
            return new ProjectBody<T>(raw, default);
        }
    }
}

/// <summary>The raw body of a request and its parsed value.</summary>
internal sealed record ProjectBody<T>(string Raw, T? Value);
