using System.Text.Json;

namespace Blindmesh.Tests.Projects;

/// <summary>
/// The bodies of the project commands. The keys are the names of the wire
/// contract, so a test never depends on a naming policy of the serializer.
/// </summary>
internal static class ProjectRequest
{
    public static string Body(params (string Key, object? Value)[] fields)
    {
        var body = fields.ToDictionary(field => field.Key, field => field.Value);

        return JsonSerializer.Serialize(body);
    }
}
