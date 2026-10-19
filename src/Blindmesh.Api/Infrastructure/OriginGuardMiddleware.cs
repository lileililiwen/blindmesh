using Blindmesh.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Infrastructure;

/// <summary>
/// Rejects unsafe requests whose Origin is not allowlisted before any side
/// effect runs. The comparison is exact; no prefix or suffix match is accepted.
/// The session bound CSRF token check is added with the session in the identity
/// package; until then a request without an Origin header is treated as a
/// non-browser client and is not blocked here.
/// </summary>
public sealed class OriginGuardMiddleware(
    RequestDelegate next,
    IOptions<ApiOptions> options,
    ILogger<OriginGuardMiddleware> logger)
{
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSafeMethod(context.Request.Method))
        {
            await next(context);
            return;
        }

        var origins = context.Request.Headers.Origin;

        if (origins.Count == 0)
        {
            await next(context);
            return;
        }

        if (origins.Count > 1)
        {
            await RejectAsync(context, "The request carried more than one Origin header.");
            return;
        }

        var origin = origins.ToString();
        var allowed = options.Value.AllowedOrigins;

        if (!allowed.Contains(origin, StringComparer.Ordinal))
        {
            await RejectAsync(context, "The request origin is not allowed.");
            return;
        }

        await next(context);
    }

    private static bool IsSafeMethod(string method) =>
        Array.Exists(SafeMethods, candidate => string.Equals(candidate, method, StringComparison.OrdinalIgnoreCase));

    private async Task RejectAsync(HttpContext context, string reason)
    {
        logger.LogWarning(
            "Rejected unsafe request {Method} {Path} with origin {Origin}: {Reason}",
            context.Request.Method,
            context.Request.Path,
            context.Request.Headers.Origin.ToString(),
            reason);

        await ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            ApiErrorWriter.Forbidden,
            "The request origin is not allowed.")
            .ExecuteAsync(context);
    }
}
