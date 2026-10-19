using System.Text.Json;

namespace Blindmesh.Api.Infrastructure;

/// <summary>
/// Converts unhandled failures into the standard error envelope. The response
/// never exposes provider, SQL or stack details; the detail is written to the
/// server log together with the request id.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception)
        {
            logger.LogInformation(exception, "Rejected malformed request {RequestId}.", RequestIdMiddleware.GetRequestId(context));

            await ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ApiErrorWriter.BadRequest,
                "The request could not be read.")
                .ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled failure {RequestId}.", RequestIdMiddleware.GetRequestId(context));

            await ApiErrorWriter.Create(
                context,
                StatusCodes.Status500InternalServerError,
                ApiErrorWriter.InternalError,
                "The request could not be completed.")
                .ExecuteAsync(context);
        }
    }
}
