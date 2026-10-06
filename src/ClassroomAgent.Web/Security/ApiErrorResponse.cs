using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ClassroomAgent.Application.Localization;
using ClassroomAgent.Application.Models.Dtos;
using Microsoft.Extensions.Localization;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The API-6 body for every error answer under <c>/api/v1</c> (US-028 api-design §2.5, §5). Each mechanism that already
/// decides an error — the cookie challenge, the restricted session, the antiforgery filter, the error page, the export
/// controller — asks <see cref="IsApi"/> and writes this body instead of a redirect or the HTML page. Messages are
/// translation keys resolved in the request's UI culture; no value, type name or detail is ever written (SC-10).
/// </summary>
public static class ApiErrorResponse
{
    public const string Prefix = "/api/v1";

    /// <summary>
    /// Cyrillic messages are written as they are (NFR-073); the encoder still escapes every HTML-sensitive character, so
    /// the body is safe even if a client renders it.
    /// </summary>
    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static bool IsApi(PathString path) => path.StartsWithSegments(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// US-028 api-design §2.5 rule (5): an <c>/api/v1</c> path that an operation serves with another method. The host's
    /// catch-all fallback (SC-4) matches such a request before routing can answer <c>405</c>, so the fallback asks here.
    /// Only literal routes exist under <c>/api/v1</c>; a route with parameters is never matched by this check.
    /// </summary>
    public static IReadOnlyList<string> MethodsServing(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (!IsApi(httpContext.Request.Path))
        {
            return [];
        }

        return httpContext.RequestServices.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.Parameters.Count == 0
                && string.Equals("/" + (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'), path, StringComparison.OrdinalIgnoreCase))
            .SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The body with one translated message and, optionally, the translated field errors.</summary>
    public static ApiError Create(
        HttpContext httpContext,
        int status,
        string messageKey,
        IReadOnlyList<(string Field, string MessageKey)>? fieldErrors = null,
        PathString? path = null)
    {
        var localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
        var clock = httpContext.RequestServices.GetRequiredService<TimeProvider>();
        return new ApiError(
            clock.GetUtcNow(),
            status,
            ErrorCode(status),
            localizer[messageKey],
            (path ?? httpContext.Request.Path).Value ?? string.Empty,
            fieldErrors?.Select(e => new ApiFieldError(e.Field, localizer[e.MessageKey])).ToList());
    }

    /// <summary>Writes the body with its status straight to the response.</summary>
    public static async Task WriteAsync(HttpContext httpContext, ApiError body, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = body.Status;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, body, JsonOptions, cancellationToken);
    }

    /// <summary>The body as an MVC result, for filters and controllers.</summary>
    public static Microsoft.AspNetCore.Mvc.JsonResult Result(ApiError body) =>
        new(body, JsonOptions) { StatusCode = body.Status, ContentType = "application/json; charset=utf-8" };

    /// <summary>API-6 <c>error</c>: a short machine-readable name of the status.</summary>
    private static string ErrorCode(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "bad_request",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status409Conflict => "conflict",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        _ => "internal_error",
    };
}
