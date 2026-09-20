namespace ClassroomAgent.Web.Security;

/// <summary>
/// Keeps the two ports apart (DC-6, SC-9). US-005 gave this middleware the rule "every request that did not arrive
/// on the private port answers <c>404</c>", because the public port served nothing yet. US-008 **replaces** that
/// rule, as US-005 wrote it expecting (api-design §7): the public port now serves the sign-in surface, and this
/// middleware keeps only the other direction — the private port serves the private paths and nothing else.
/// </summary>
/// <remarks>
/// The inverse — a private path requested on the public port — is refused by
/// <see cref="PrivatePortEndpointFilter"/> on the route group itself. The port is the connection's actual local
/// port; <c>Host</c> and forwarded headers play no part, because a reverse proxy makes them forgeable.
/// </remarks>
public sealed class PublicPortMiddleware(RequestDelegate next, int privatePort)
{
    private static readonly PathString[] PrivatePaths =
    [
        new("/health"),
        new("/" + ClassroomAgent.Contracts.ServiceChannel.StatusPushPath),
    ];

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Connection.LocalPort != privatePort || IsPrivatePath(context.Request.Path))
        {
            return next(context);
        }

        // The private port carries status only: the school's pages are not served on it.
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    private static bool IsPrivatePath(PathString path) =>
        PrivatePaths.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
