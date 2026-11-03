using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The one rule of the installation header's two controls, so they cannot drift (US-071 spec FR-010, api-design D-3). The
/// endpoint cannot tell an error page: <c>ErrorController.Page</c> answers under the protected endpoint that failed. Every
/// path to that page uses its model, so no caller has to remember a flag.
/// </summary>
public static class PageHeaderRules
{
    /// <summary>Api-design D-4: the header field's value; set only by the results page, and only with a validated text.</summary>
    public const string SearchTextViewDataKey = "HeaderSearchText";

    public static bool IsErrorPage(ViewDataDictionary viewData)
    {
        ArgumentNullException.ThrowIfNull(viewData);
        return viewData.Model is ErrorPageModel;
    }

    /// <summary>US-039 FR-002, US-071 FR-010: signed in, the endpoint requires sign-in, not the error page.</summary>
    public static bool ShowsLanguageSwitcher(HttpContext context, ViewDataDictionary viewData)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.User.Identity?.IsAuthenticated == true
            && context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null
            && !IsErrorPage(viewData);
    }

    /// <summary>US-071 FR-001: the same, and the session holds no temporary password claim.</summary>
    public static bool ShowsSearchField(HttpContext context, ViewDataDictionary viewData)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ShowsLanguageSwitcher(context, viewData)
            && context.User.FindFirst(InstallationClaimTypes.PasswordIsTemporary) is null;
    }
}
