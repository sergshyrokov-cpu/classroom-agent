using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// Re-applies the culture of the current request for the duration of a scope, and restores the previous one
/// on dispose (US-009; NFR-073).
/// </summary>
/// <remarks>
/// Needed by code that runs <b>outside</b> the localization middleware — the host's exception handler, which
/// sits in the outermost middleware. <c>CurrentUICulture</c> is ambient to the async scope that set it, so it
/// is gone by the time an exception has unwound that far, while <see cref="IRequestCultureFeature"/> stays on
/// the <c>HttpContext</c>. Without this the translated error page would come out in the server's culture.
/// </remarks>
public sealed class RequestCultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private bool _disposed;

    private RequestCultureScope(CultureInfo culture, CultureInfo uiCulture)
    {
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = uiCulture;
    }

    /// <summary>Applies the request's culture; a no-op scope when the request carries none.</summary>
    public static RequestCultureScope Apply(HttpContext httpContext)
    {
        var feature = httpContext.Features.Get<IRequestCultureFeature>();
        return feature is null
            ? new RequestCultureScope(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture)
            : new RequestCultureScope(feature.RequestCulture.Culture, feature.RequestCulture.UICulture);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}
