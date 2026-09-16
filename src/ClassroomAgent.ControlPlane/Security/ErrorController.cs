using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// The host's one error page for 400, 403, 404 and 500: translated text only, no detail,
/// reads and writes nothing (FR-016, SC-4). Reached directly or by re-execution, with
/// any method; any other code answers 404.
/// </summary>
[AllowAnonymous]
[SetupGateExempt]
public sealed class ErrorController : Controller
{
    [Route("error/{statusCode}")]
    public IActionResult Index(string statusCode) =>
        Page(HttpContext, int.TryParse(statusCode, out var code) ? code : StatusCodes.Status404NotFound, backLink: null);

    /// <summary>The error page as a result, for callers outside this controller (antiforgery refusal).</summary>
    public static ViewResult Page(HttpContext httpContext, int statusCode, string? backLink)
    {
        var (status, messageKey) = statusCode switch
        {
            StatusCodes.Status400BadRequest => (statusCode, "Error.PageExpired"),
            StatusCodes.Status403Forbidden => (statusCode, "Error.Forbidden"),
            StatusCodes.Status500InternalServerError => (statusCode, "Error.Internal"),
            _ => (StatusCodes.Status404NotFound, "Error.NotFound"),
        };

        var metadataProvider = httpContext.RequestServices.GetRequiredService<IModelMetadataProvider>();
        return new ViewResult
        {
            ViewName = "~/Views/Error/Index.cshtml",
            StatusCode = status,
            ViewData = new ViewDataDictionary<ErrorPageModel>(metadataProvider, new ModelStateDictionary())
            {
                Model = new ErrorPageModel(messageKey, status == StatusCodes.Status400BadRequest ? backLink ?? "/" : null),
            },
        };
    }
}
