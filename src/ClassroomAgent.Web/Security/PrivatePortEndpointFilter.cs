namespace ClassroomAgent.Web.Security;

/// <summary>
/// The filter of the private route group (DC-6): the route answers only when the connection's local port is
/// the configured private port, and <c>404</c> otherwise. <c>Host</c> and <c>X-Forwarded-Host</c> play no part.
/// </summary>
public sealed class PrivatePortEndpointFilter(int privatePort) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Connection.LocalPort == privatePort
            ? next(context)
            : ValueTask.FromResult<object?>(Results.NotFound());
}
