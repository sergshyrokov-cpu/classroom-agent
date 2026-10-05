using Microsoft.AspNetCore.StaticFiles;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// The one host-wide rule of US-040 (SC-14, <c>trebovaniya.md</c> §8 v85): every response of the Control Plane carries
/// <c>Cache-Control: no-store</c>, so a browser on a shared computer keeps no copy of a journal or report after
/// sign-out. Registered first in the pipeline; the header is decided when the response starts, so redirects,
/// the setup gate, refusals and the error page written further down are covered too (api-design D-3).
/// </summary>
/// <remarks>
/// A value that already carries <c>no-store</c> — antiforgery's <c>no-cache, no-store</c>, the exception handler's
/// <c>no-cache,no-store</c> — is kept (spec FR-006, D-1). The single exception is a file the static-file middleware
/// served, which it marks through <see cref="MarkStaticFile"/> (spec FR-004, D-4).
/// </remarks>
public sealed class NoStoreMiddleware(RequestDelegate next)
{
    private const string NoStore = "no-store";

    private static readonly object StaticFileServed = new();

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            Apply(context);
            return Task.CompletedTask;
        });
        return next(context);
    }

    /// <summary>The <c>OnPrepareResponse</c> hook of the static-file middleware: this response is a served file.</summary>
    public static void MarkStaticFile(StaticFileResponseContext context) => context.Context.Items[StaticFileServed] = true;

    private static void Apply(HttpContext context)
    {
        if (context.Items.ContainsKey(StaticFileServed))
        {
            return;
        }

        var current = context.Response.Headers.CacheControl.ToString();
        if (!current.Split(',').Any(d => string.Equals(d.Trim(), NoStore, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Headers.CacheControl = NoStore;
        }
    }
}
