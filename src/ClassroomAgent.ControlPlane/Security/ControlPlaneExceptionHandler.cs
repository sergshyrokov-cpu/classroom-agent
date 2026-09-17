using Microsoft.AspNetCore.Diagnostics;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// The host's single exception handler (AD-9, API-10): logs the failure and leaves the
/// response to the re-executed <c>/error/500</c> page, which shows no detail (SC-10). A service-channel
/// request gets <c>500</c> with an empty body instead (US-005 api-design §5, §13).
/// </summary>
public sealed class ControlPlaneExceptionHandler(ILogger<ControlPlaneExceptionHandler> logger) : IExceptionHandler
{
    private static readonly PathString ServiceChannelPrefix = new("/service");

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception while processing the request");
        if (!httpContext.Request.Path.StartsWithSegments(ServiceChannelPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(false);
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return ValueTask.FromResult(true);
    }
}
