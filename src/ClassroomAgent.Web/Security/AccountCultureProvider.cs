using Microsoft.AspNetCore.Localization;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The only culture sources of this Story: the signed-in account's stored language, otherwise the school default
/// from configuration (US-008 spec FR-017, I-14). <c>Accept-Language</c>, the query string and cookies are not
/// consulted — NFR-073 names exactly those two sources, and US-039 adds the user's own choice.
/// </summary>
public sealed class AccountCultureProvider(string schoolDefault) : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var language = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(InstallationClaimTypes.UiLanguage)?.Value
            : null;

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(language ?? schoolDefault));
    }
}
