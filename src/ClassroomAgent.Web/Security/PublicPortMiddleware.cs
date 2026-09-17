namespace ClassroomAgent.Web.Security;

/// <summary>
/// The public port maps nothing in US-005 (spec FR-014, I-13): every request that did not arrive on the private
/// port answers <c>404</c> with an empty body — no cookie, no redirect, nothing read or written. The port is the
/// connection's actual local port; <c>Host</c> and forwarded headers play no part (DC-6). The host baseline of
/// the public port arrives with US-008.
/// </summary>
public sealed class PublicPortMiddleware(RequestDelegate next, int privatePort)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Connection.LocalPort == privatePort)
        {
            return next(context);
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
