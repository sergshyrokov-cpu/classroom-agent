using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The host's one error page for <c>400</c>, <c>403</c>, <c>404</c>, <c>409</c> and <c>500</c>: translated text
/// only, no detail, reads and writes nothing (US-008 spec FR-018; SC-4). Reached directly or by re-execution, with
/// any method; any other code answers <c>404</c>.
/// </summary>
[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [Route("error/{statusCode}")]
    public IActionResult Index(string statusCode) =>
        Page(HttpContext, int.TryParse(statusCode, out var code) ? code : StatusCodes.Status404NotFound, backLink: null);

    /// <summary>The error page as a result, for callers outside this controller: the antiforgery refusal and the
    /// read-only refusal that reaches HTTP (spec FR-005, FR-014).</summary>
    public static ViewResult Page(HttpContext httpContext, int statusCode, string? backLink, string? textKey = null)
    {
        var (status, messageKey) = statusCode switch
        {
            StatusCodes.Status400BadRequest => (statusCode, "Error.PageExpired"),
            StatusCodes.Status403Forbidden => (statusCode, "Error.Forbidden"),
            StatusCodes.Status409Conflict => (statusCode, textKey ?? "Error.Unexpected"),
            StatusCodes.Status500InternalServerError => (statusCode, "Error.Unexpected"),
            _ => (StatusCodes.Status404NotFound, "Error.NotFound"),
        };

        var metadataProvider = httpContext.RequestServices.GetRequiredService<IModelMetadataProvider>();
        return new ViewResult
        {
            ViewName = "~/Views/Error/Index.cshtml",
            StatusCode = status,
            ViewData = new ViewDataDictionary<ErrorPageModel>(metadataProvider, new ModelStateDictionary())
            {
                Model = new ErrorPageModel(
                    messageKey,
                    status == StatusCodes.Status400BadRequest ? backLink ?? SignInRoutes.Landing : null),
            },
        };
    }
}
