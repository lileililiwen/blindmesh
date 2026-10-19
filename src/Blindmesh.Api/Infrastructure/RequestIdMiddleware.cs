using System.Diagnostics;

namespace Blindmesh.Api.Infrastructure;

/// <summary>
/// Assigns a request id to every request and echoes it in the response header and
/// in the error envelope. A caller supplied id is accepted only when it is short
/// printable ASCII, so a hostile value cannot pollute the logs.
/// </summary>
public sealed class RequestIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Request-Id";
    public const string ItemKey = "blindmesh.request_id";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = ResolveRequestId(context.Request.Headers[HeaderName]);
        context.Items[ItemKey] = requestId;
        context.Response.Headers[HeaderName] = requestId;

        Activity.Current?.SetTag("request.id", requestId);

        await next(context);
    }

    public static string GetRequestId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string requestId
            ? requestId
            : context.TraceIdentifier;

    private static string ResolveRequestId(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue) || headerValue.Length > 100)
        {
            return Guid.NewGuid().ToString("N");
        }

        foreach (var character in headerValue)
        {
            if (character is < (char)0x21 or > (char)0x7E)
            {
                return Guid.NewGuid().ToString("N");
            }
        }

        return headerValue;
    }
}
