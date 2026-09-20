using System.Text.Json;
using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Localization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace ClassroomAgent.Web.Exceptions;

/// <summary>
/// The host's single exception handler (US-008 spec FR-014; AD-9, API-10). It closes what US-007 recorded as
/// outstanding: a <see cref="ReadOnlyModeException"/> that reaches HTTP becomes <c>409</c> — the API-6 body under
/// <c>/api/v1</c>, the error page elsewhere — naming the reason in the user's language. <b>API-5 is satisfied end
/// to end from this Story onwards.</b>
/// </summary>
/// <remarks>
/// Every other unhandled exception is left to the re-executed <c>/error/500</c> page, which shows no detail: no
/// stack trace, no SQL, no class or namespace name, no file path, no Google error, no secret (NFR-023, SC-10).
/// </remarks>
public sealed class InstallationExceptionHandler(
    IStringLocalizer<SharedResource> localizer,
    TimeProvider timeProvider,
    ILogger<InstallationExceptionHandler> logger) : IExceptionHandler
{
    private const string ApiPrefix = "/api/v1";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ReadOnlyModeException refusal)
        {
            logger.LogError(exception, "Unhandled exception while processing the request");
            return false;
        }

        var textKey = ReasonKey(refusal.Reason);
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        // US-009: restore the request's culture before any string is resolved. This handler runs in the outermost
        // middleware, and the culture the localization middleware set deeper in the pipeline does not flow back
        // out of that scope — without this the refusal would be rendered in the server's culture, not the user's
        // (NFR-073). The feature itself does survive on HttpContext, so the decision of AccountCultureProvider is
        // reused rather than made a second time.
        using var culture = RequestCultureScope.Apply(httpContext);

        if (httpContext.Request.Path.StartsWithSegments(ApiPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await WriteApiErrorAsync(httpContext, textKey, cancellationToken);
            return true;
        }

        await RenderErrorPageAsync(httpContext, textKey);
        return true;
    }

    /// <summary>BR-025's three reasons, as the translation keys presentation resolves (spec FR-014; AD-6).</summary>
    private static string ReasonKey(LegitimacyModeReason reason) => reason switch
    {
        LegitimacyModeReason.NotYetConfirmed => "ReadOnly.Refused.NotYetConfirmed",
        LegitimacyModeReason.SuspendedByOwner => "ReadOnly.Refused.SuspendedByOwner",
        LegitimacyModeReason.GracePeriodExpired => "ReadOnly.Refused.GracePeriodExpired",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    private async Task WriteApiErrorAsync(HttpContext httpContext, string textKey, CancellationToken cancellationToken)
    {
        var body = new ApiError(
            timeProvider.GetUtcNow(),
            StatusCodes.Status409Conflict,
            "conflict",
            localizer[textKey],
            httpContext.Request.Path.Value ?? string.Empty);

        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, body, JsonOptions, cancellationToken);
    }

    /// <summary>
    /// The same error page every other failure gets, with the read-only reason as its text. Executed here rather
    /// than re-entered through status-code re-execution, because the reason travels as data and would otherwise
    /// have to be smuggled into a second request.
    /// </summary>
    private static async Task RenderErrorPageAsync(HttpContext httpContext, string textKey)
    {
        var page = ErrorController.Page(httpContext, StatusCodes.Status409Conflict, backLink: null, textKey);
        var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor());
        await page.ExecuteResultAsync(actionContext);
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);
}
