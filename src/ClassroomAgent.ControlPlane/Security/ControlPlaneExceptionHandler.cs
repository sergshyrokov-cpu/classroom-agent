using Microsoft.AspNetCore.Diagnostics;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// The host's single exception handler (AD-9, API-10): logs the failure and leaves the
/// response to the re-executed <c>/error/500</c> page, which shows no detail (SC-10).
/// </summary>
public sealed class ControlPlaneExceptionHandler(ILogger<ControlPlaneExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception while processing the request");
        return ValueTask.FromResult(false);
    }
}
