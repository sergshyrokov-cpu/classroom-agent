using ClassroomAgent.ControlPlane.Services;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// While no Owner account exists, redirects every request to a non-exempt endpoint to
/// <c>/setup</c> — before authentication challenges and antiforgery (FR-001). Requests
/// that match no endpoint are left to the 404 handling. The rule is host-wide, so a
/// later endpoint is covered without its own check.
/// </summary>
public sealed class SetupGateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, OwnerSessionService sessions)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null
            || endpoint.Metadata.GetMetadata<SetupGateExemptAttribute>() is not null
            || await sessions.OwnerExistsAsync(context.RequestAborted))
        {
            await next(context);
            return;
        }

        context.Response.Redirect("/setup");
    }
}
