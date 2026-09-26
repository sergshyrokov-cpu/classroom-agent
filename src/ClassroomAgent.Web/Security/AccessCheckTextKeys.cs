using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of "Проверить доступ" (US-011 spec FR-012; test strategy §5). Every sentence of the page is
/// one of these; scope URIs, the technical account and the domain are data and are never translated.
/// </summary>
public static class AccessCheckTextKeys
{
    public const string Title = "AccessCheck.Title";

    public const string Explanation = "AccessCheck.Explanation";

    public const string RunButton = "AccessCheck.Run.Button";

    public const string TechnicalAccountLabel = "AccessCheck.TechnicalAccount.Label";

    public const string StepClassroomRead = "AccessCheck.Step.ClassroomRead";

    public const string StepReportsRead = "AccessCheck.Step.ReportsRead";

    public const string RefusedNotConfigured = "AccessCheck.Refused.NotConfigured";

    public const string RefusedDomainMismatch = "AccessCheck.Refused.DomainMismatch";

    /// <summary>The entry of the landing page's settings section (spec FR-007).</summary>
    public const string NavigationEntry = "Landing.Settings.AccessCheck";

    public static string Of(AccessCheckVerdict verdict) => "AccessCheck.Verdict." + verdict;

    public static string Of(AccessCheckStepOutcome outcome) => "AccessCheck.Outcome." + outcome;

    public static string Of(AccessCheckRefusal refusal) => refusal switch
    {
        AccessCheckRefusal.NotConfigured => RefusedNotConfigured,
        AccessCheckRefusal.DomainMismatch => RefusedDomainMismatch,
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null),
    };
}
