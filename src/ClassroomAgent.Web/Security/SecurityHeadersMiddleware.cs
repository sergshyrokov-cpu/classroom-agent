using Microsoft.AspNetCore.Authentication.Google;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The one host-wide rule of US-050 (SC-15, <c>trebovaniya.md</c> §8 v95): every response of both ports — static
/// files, the error page, downloaded files and <c>/api/v1</c> responses included — carries the four browser security
/// headers. Registered first in the pipeline; the headers are written when the response starts, so redirects,
/// refusals and the error page written further down are covered too (spec FR-002).
/// </summary>
/// <remarks>
/// Each header is set, not appended: antiforgery's <c>X-Frame-Options: SAMEORIGIN</c> is replaced by <c>DENY</c>, so
/// every header occurs exactly once (spec FR-005, I-4).
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// The origin of the Google authorization endpoint the "Sign in with Google" form is redirected to. The handler
    /// keeps the default endpoint (<see cref="InstallationSecurityServices"/>), so the origin is taken from it
    /// (spec FR-004, I-2).
    /// </summary>
    private static readonly string GoogleSignInOrigin =
        new Uri(GoogleDefaults.AuthorizationEndpoint).GetLeftPart(UriPartial.Authority);

    private static readonly string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; "
        + "object-src 'none'; base-uri 'self'; "
        + $"form-action 'self' {GoogleSignInOrigin}; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            Apply(context.Response.Headers);
            return Task.CompletedTask;
        });
        return next(context);
    }

    private static void Apply(IHeaderDictionary headers)
    {
        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "same-origin";
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
    }
}
