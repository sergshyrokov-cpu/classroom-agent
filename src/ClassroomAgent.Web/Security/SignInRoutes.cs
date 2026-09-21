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
