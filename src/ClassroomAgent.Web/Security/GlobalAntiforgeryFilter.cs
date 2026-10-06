using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// Antiforgery validation for every POST, PUT, PATCH and DELETE of the public port, anonymous forms included
/// (US-008 spec FR-005; SC-4, API-7). The installation's public-port exemption list is <b>empty</b>: the service
/// channel's exemptions live on the private port, and the OAuth callback needs none because it is a GET (v64).
/// A refusal is <c>400</c> with the translated "page is out of date" text and a link back to the same page.
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
            // US-028 api-design §2.5: a script under /api/v1 gets the API-6 body with the same message.
            if (ApiErrorResponse.IsApi(httpContext.Request.Path))
            {
                context.Result = ApiErrorResponse.Result(
                    ApiErrorResponse.Create(httpContext, StatusCodes.Status400BadRequest, "Error.PageExpired"));
                return;
            }

            context.Result = ErrorController.Page(httpContext, StatusCodes.Status400BadRequest, BackLink(httpContext.Request.Path));
        }
    }

    /// <summary>The page the form came from: a local path, never an absolute URL (api-design §3).</summary>
    private static string BackLink(PathString path) =>
        path.StartsWithSegments(SignInRoutes.SignInPage, StringComparison.OrdinalIgnoreCase)
            ? SignInRoutes.SignInPage
            : SignInRoutes.Landing;
}
