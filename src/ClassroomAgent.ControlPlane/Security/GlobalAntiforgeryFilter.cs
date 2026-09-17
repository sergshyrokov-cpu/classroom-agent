using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// Antiforgery validation for every POST, PUT, PATCH and DELETE of the host, anonymous
/// forms included (SC-4, API-7). The only exemption is the SC-4 closed list, marked with
/// <see cref="IgnoreAntiforgeryTokenAttribute"/>: the legitimacy check called by installations (US-005). A refusal is <c>400</c> with the
/// translated "page expired" error page and a link back (FR-015). The internal
/// re-execution of an already-validated request to the error page is not validated again.
/// </summary>
public sealed class GlobalAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        if (SafeMethods.Contains(httpContext.Request.Method, StringComparer.OrdinalIgnoreCase)
            || httpContext.Features.Get<IStatusCodeReExecuteFeature>() is not null
            || httpContext.Features.Get<IExceptionHandlerPathFeature>() is not null
            || context.ActionDescriptor.EndpointMetadata.OfType<IgnoreAntiforgeryTokenAttribute>().Any())
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = ErrorController.Page(httpContext, StatusCodes.Status400BadRequest, BackLink(httpContext.Request.Path));
        }
    }

    private static string BackLink(PathString path) =>
        path.Equals("/setup", StringComparison.OrdinalIgnoreCase) || path.Equals("/sign-in", StringComparison.OrdinalIgnoreCase)
            ? path.Value!.ToLowerInvariant()
            : "/";
}
