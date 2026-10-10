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

    /// <summary>
    /// US-025 openapi: the journal of one course for a period, in the Google Workspace section (api-design §2.1).
    /// GET only; the form submits to it.
    /// </summary>
    public const string Journal = "/workspace/journal";

    /// <summary>US-032 api-design §2.1: the Meet meetings page, one list at a time. GET only.</summary>
    public const string MeetCodes = "/workspace/meet-codes";

    /// <summary>US-032: the course-choice form for one code (GET).</summary>
    public const string MeetCodeCourseChoice = "/workspace/meet-codes/{meetingCode}/course-choice";

    /// <summary>US-032: the code's link to a course — pick, re-link, remove the mark (POST).</summary>
    public const string MeetCodeLink = "/workspace/meet-codes/{meetingCode}/link";

    /// <summary>US-032: confirming an automatic link (POST).</summary>
    public const string MeetCodeConfirmation = "/workspace/meet-codes/{meetingCode}/confirmation";

    /// <summary>US-032: marking a code "not a course" (POST).</summary>
    public const string MeetCodeNotACourseMark = "/workspace/meet-codes/{meetingCode}/not-a-course-mark";

    /// <summary>US-032 api-design §2.7: the key the one-time confirmation travels under in TempData.</summary>
    public const string MeetCodeMessageTempDataKey = "MeetCodeMessage";

    /// <summary>US-027 api-design §2.1: the report templates list (the section's entry page) and the create save.</summary>
    public const string ReportTemplates = "/reports/templates";

    public const string ReportTemplateNew = "/reports/templates/new";

    public const string ReportTemplateCopy = "/reports/templates/{templateRef}/copy";

    public const string ReportTemplateEdit = "/reports/templates/{templateRef}/edit";

    /// <summary>US-027: the change save; the same path takes no GET.</summary>
    public const string ReportTemplateChange = "/reports/templates/{templateRef}";

    /// <summary>US-027 api-design §2.4: the delete confirmation page (GET) and the delete (POST).</summary>
    public const string ReportTemplateDeletion = "/reports/templates/{templateRef}/deletion";

    /// <summary>US-027 api-design §2.1: the on-screen report; GET only, the form submits to it.</summary>
    public const string Report = "/reports";

    /// <summary>US-027: the key the template confirmation travels under in TempData (api-design §2.5).</summary>
    public const string ReportTemplateMessageTempDataKey = "ReportTemplateMessage";

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
