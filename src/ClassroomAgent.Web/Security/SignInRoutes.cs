using ClassroomAgent.Application.Ports;
using Microsoft.AspNetCore.Http;

namespace ClassroomAgent.Web.Security;

/// <summary>The sign-in paths of US-008 openapi, in one place so the handler and the controllers cannot drift.</summary>
public static class SignInRoutes
{
    public const string SignInPage = "/sign-in";

    public const string Start = "/sign-in/google";

    public const string Callback = "/signin-google";

    public const string SignOut = "/sign-out";

    public const string Landing = "/";

    /// <summary>US-009 openapi: the connection settings page and its save share one path.</summary>
    public const string WorkspaceConnection = "/settings/workspace-connection";

    /// <summary>
    /// US-010 openapi: the super-admin instruction. Singular, because there is exactly one instruction per
    /// <c>Installation</c> — API-3's plural rule is about collections (api-design §2.2). GET only.
    /// </summary>
    public const string ConnectionInstruction = "/settings/connection-instruction";

    /// <summary>US-011 openapi: the check-access page and its run, one path (api-design §2.1).</summary>
    public const string AccessCheck = "/settings/access-check";

    /// <summary>
    /// US-019 openapi: the manual synchronization request, one path for Admin and Dean — outside <c>/settings</c>,
    /// which is the Admin's section (api-design §2.1). POST only.
    /// </summary>
    public const string SynchronizationRequests = "/synchronization/requests";

    /// <summary>US-012 openapi: the Admin's Dean accounts screen, its list and its creation form.</summary>
    public const string Deans = "/settings/deans";

    /// <summary>US-012 openapi: disabling and re-enabling one account — one operation, both directions.</summary>
    public const string DeanState = "/settings/deans/{deanId:long}/state";

    /// <summary>US-012 openapi: resetting one account's password to a new temporary one.</summary>
    public const string DeanPassword = "/settings/deans/{deanId:long}/password";

    /// <summary>US-012 openapi: the forced change of a temporary password (api-design §2.5, §2.6).</summary>
    public const string ForcedPasswordChange = "/sign-in/change-password";

    /// <summary>US-012 openapi: the Dean's own password page.</summary>
    public const string OwnPassword = "/account/password";

    /// <summary>US-039: the signed-in user's own language choice (openapi <c>POST /account/language</c>).</summary>
    public const string Language = "/account/language";

    /// <summary>The key the US-012 message travels under in TempData — never a query parameter (the US-008 rule).</summary>
    public const string MessageTempDataKey = "DeanAccountMessage";

    /// <summary>The key the refusal category travels under in TempData (api-design §2.2). Never a query parameter.</summary>
    public const string RefusalTempDataKey = "SignInRefusal";

    /// <summary>
    /// The installation's Workspace domain as the last successful legitimacy check reported it, or null while none
    /// has ever succeeded (OD-002). Read through <c>Application</c> from the state US-005 stores; the sign-in path
    /// never asks the Control Plane for it (spec FR-006).
    /// </summary>
    public static async Task<string?> KnownDomainAsync(HttpContext httpContext)
    {
        var states = httpContext.RequestServices.GetRequiredService<ILegitimacyStateRepository>();
        var state = await states.GetForReadAsync(httpContext.RequestAborted);
        return string.IsNullOrEmpty(state?.Domain) ? null : state.Domain;
    }
}
