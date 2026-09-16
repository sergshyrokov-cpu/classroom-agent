using Microsoft.AspNetCore.Localization;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// The only culture source of this Story: the signed-in Owner's account language.
/// Anonymous requests fall back to the default, Ukrainian; <c>Accept-Language</c>, query
/// string and cookies are not consulted (FR-019).
/// </summary>
public sealed class OwnerAccountCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var language = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(OwnerClaimTypes.UiLanguage)?.Value
            : null;

        return Task.FromResult(language is null ? null : new ProviderCultureResult(language));
    }
}
