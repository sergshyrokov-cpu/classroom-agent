using System.Security.Claims;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// What happens when Google returns (US-008 spec FR-007, FR-010; api-design §3). The handler has already proven the
/// OAuth <c>state</c> parameter and the correlation cookie by the time <see cref="OnTicketReceivedAsync"/> runs, and
/// <see cref="OnRemoteFailureAsync"/> is where a missing, unknown or replayed <c>state</c>, an absent correlation
/// cookie or a cancelled consent arrives.
/// </summary>
/// <remarks>
/// The decision itself is the use case's; this class only carries the address to it, issues the session on success,
/// and turns a refusal into a redirect with its category in TempData (api-design §2.2). No Google token is kept
/// (S-09), and the session is issued only after the use case returned success (spec FR-010).
/// </remarks>
public static class GoogleSignInEvents
{
    public static async Task OnTicketReceivedAsync(TicketReceivedContext context)
    {
        var httpContext = context.HttpContext;
        var useCase = httpContext.RequestServices.GetRequiredService<CompleteGoogleSignInUseCase>();
        var requestId = httpContext.TraceIdentifier;
        var principal = context.Principal;

        // VR-005, spec I-6: the email must be present, syntactically valid and flagged verified by Google. A failure
        // of any of these is a failed callback, never "not in AllowedAdmin" — it is a defect of the identity.
        var email = principal?.FindFirstValue(ClaimTypes.Email);
        var verified = principal?.FindFirstValue(InstallationClaimTypes.EmailVerified) == "true";
        var outcome = IsUsable(email) && verified
            ? await useCase.ExecuteAsync(email!, requestId, httpContext.RequestAborted)
            : await useCase.RecordFailedCallbackAsync(requestId, httpContext.RequestAborted);

        context.HandleResponse();
        var logger = Logger(httpContext);
        if (outcome is { Succeeded: true, User: { } user })
        {
            SignInLog.Succeed(logger, user.Id, requestId);
            await InstallationSession.SignInAsync(
                httpContext,
                user,
                httpContext.RequestServices.GetRequiredService<TimeProvider>());
            httpContext.Response.Redirect(SignInRoutes.Landing);
            return;
        }

        SignInLog.Refuse(logger, outcome.Refusal!.Value, requestId);
        Refuse(httpContext, outcome.Refusal!.Value);
    }

    /// <summary>
    /// The callback itself failed before a trustworthy identity existed (VR-007). Nothing is created, the Control
    /// Plane is not asked, and the audit row records the category "the callback itself failed".
    /// </summary>
    public static async Task OnRemoteFailureAsync(RemoteFailureContext context)
    {
        var httpContext = context.HttpContext;
        var useCase = httpContext.RequestServices.GetRequiredService<CompleteGoogleSignInUseCase>();
        var requestId = httpContext.TraceIdentifier;
        var outcome = await useCase.RecordFailedCallbackAsync(requestId, httpContext.RequestAborted);

        context.HandleResponse();
        SignInLog.Refuse(Logger(httpContext), outcome.Refusal!.Value, requestId);
        Refuse(httpContext, outcome.Refusal!.Value);
    }

    private static ILogger Logger(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(GoogleSignInEvents).FullName!);

    private static void Refuse(HttpContext httpContext, SignInRefusal refusal)
    {
        var tempData = httpContext.RequestServices
            .GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(httpContext);
        tempData[SignInRoutes.RefusalTempDataKey] = TextKey(refusal);
        tempData.Save();
        httpContext.Response.Redirect(SignInRoutes.SignInPage);
    }

    /// <summary>The refusal as a translation key; the categories are not distinguished beyond what the text says (S-14).</summary>
    private static string TextKey(SignInRefusal refusal) => refusal switch
    {
        SignInRefusal.NotApproved => "SignIn.Refused.NotApproved",
        SignInRefusal.CouldNotConfirm => "SignIn.Refused.CouldNotConfirm",
        SignInRefusal.SignInFailed => "SignIn.Refused.SignInFailed",
        SignInRefusal.AccountDisabled => "SignIn.Refused.AccountDisabled",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null),
    };

    private static bool IsUsable(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at < email.Length - 1 && email.IndexOf('@', at + 1) < 0 && !email.Any(char.IsWhiteSpace);
    }
}
