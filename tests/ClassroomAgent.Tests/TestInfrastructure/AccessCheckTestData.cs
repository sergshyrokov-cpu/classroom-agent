using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Paths, the policy, the page markers, audit codes, log event names and translation keys of US-011 (openapi,
/// api-design, db-design §3.2; spec FR-007…FR-016). The path and the policy are the api-design's; the audit codes
/// are the db-design's; the markup markers, the setting name, the log event names and the translation keys are
/// fixed here by TEST_WRITING, as US-009 and US-010 fixed theirs — IMPLEMENTATION honours them or corrects them
/// together with the tests.
/// </summary>
public static class AccessCheckTestData
{
    /// <summary>The one path of US-011 openapi, serving both the page and the run.</summary>
    public const string Path = "/settings/access-check";

    /// <summary>api-design §2.7: its own policy for its own matrix row.</summary>
    public const string Policy = "RunAccessCheck";

    /// <summary>
    /// Spec FR-016: the setting that names the service-account key in the secret store. The name is fixed here in
    /// the style of <c>GoogleOAuth:ClientSecretReference</c>.
    /// </summary>
    public const string KeyReferenceSetting = "Google:ServiceAccountKeyReference";

    /// <summary>The technical account a usable connection carries (US-009 test data).</summary>
    public const string TechnicalAccount = WorkspaceConnectionTestData.TechnicalAccount;

    /// <summary>The six scopes, in the order of <c>GoogleDelegationScopes.All</c> (openapi <c>x-scopes-in-order</c>).</summary>
    public static IReadOnlyList<string> Scopes => ConnectionInstructionTestData.Scopes;

    /// <summary>What must never be requested (openapi <c>x-never</c>).</summary>
    public static IReadOnlyList<string> ForbiddenScopeFragments => ConnectionInstructionTestData.ForbiddenScopeFragments;

    /// <summary>Scopes as a theory source.</summary>
    public static TheoryData<string> ScopeList
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var scope in Scopes)
            {
                data.Add(scope);
            }

            return data;
        }
    }

    /// <summary>
    /// Markup markers of the result (api-design §2.5). One element carries the verdict; one element per step carries
    /// its kind, its scope (delegation steps only) and its outcome as data attributes, so a test reads exactly the
    /// eight steps the view model holds.
    /// </summary>
    public static class Markup
    {
        public const string ResultElementId = "access-check-result";

        public const string VerdictElementId = "access-check-verdict";

        public const string VerdictAttribute = "data-verdict";

        public const string StepClass = "access-check-step";

        public const string KindAttribute = "data-kind";

        public const string ScopeAttribute = "data-scope";

        public const string OutcomeAttribute = "data-outcome";
    }

    /// <summary>The verdicts of openapi <c>AccessCheckVerdict</c>.</summary>
    public static class Verdict
    {
        public const string AccessInPlace = "AccessInPlace";

        public const string NotConfigured = "NotConfigured";

        public const string Inconclusive = "Inconclusive";
    }

    /// <summary>The step kinds of openapi <c>AccessCheckStep.kind</c>.</summary>
    public static class Kind
    {
        public const string Delegation = "Delegation";

        public const string ClassroomRead = "ClassroomRead";

        public const string ReportsRead = "ReportsRead";
    }

    /// <summary>db-design §3.2.</summary>
    public static class Audit
    {
        public const string Action = "access_check_run";

        public const string TargetType = "workspace_connection";

        public const string ReadOnlyMode = "read_only_mode";

        public const string ConnectionNotUsable = "connection_not_usable";

        public const string Succeeded = "succeeded";

        public const string Refused = "refused";
    }

    /// <summary>Log event names of the startup self-check (spec FR-010, OD-004, I-6).</summary>
    public static class LogEvents
    {
        /// <summary>The self-check ran: <c>Information</c> when access is in place, <c>Error</c> otherwise.</summary>
        public const string SelfCheckCompleted = "AccessSelfCheckCompleted";

        /// <summary>The self-check did not run, with the reason, at <c>Warning</c>.</summary>
        public const string SelfCheckSkipped = "AccessSelfCheckSkipped";
    }

    /// <summary>The translation keys this Story adds (spec FR-012). Both files carry every one.</summary>
    public static class TextKeys
    {
        public const string Title = "AccessCheck.Title";

        public const string Explanation = "AccessCheck.Explanation";

        public const string RunButton = "AccessCheck.Run.Button";

        public const string TechnicalAccountLabel = "AccessCheck.TechnicalAccount.Label";

        public const string VerdictAccessInPlace = "AccessCheck.Verdict.AccessInPlace";

        public const string VerdictNotConfigured = "AccessCheck.Verdict.NotConfigured";

        public const string VerdictInconclusive = "AccessCheck.Verdict.Inconclusive";

        public const string StepClassroomRead = "AccessCheck.Step.ClassroomRead";

        public const string StepReportsRead = "AccessCheck.Step.ReportsRead";

        public const string OutcomeSucceeded = "AccessCheck.Outcome.Succeeded";

        public const string OutcomeScopeNotAuthorized = "AccessCheck.Outcome.ScopeNotAuthorized";

        public const string OutcomeTechnicalAccountUnknown = "AccessCheck.Outcome.TechnicalAccountUnknown";

        public const string OutcomeTechnicalAccountCannotRead = "AccessCheck.Outcome.TechnicalAccountCannotRead";

        public const string OutcomeApiNotEnabled = "AccessCheck.Outcome.ApiNotEnabled";

        public const string OutcomeKeyUnavailable = "AccessCheck.Outcome.KeyUnavailable";

        public const string OutcomeKeyRejected = "AccessCheck.Outcome.KeyRejected";

        public const string OutcomeGoogleUnavailable = "AccessCheck.Outcome.GoogleUnavailable";

        public const string OutcomeNotAttempted = "AccessCheck.Outcome.NotAttempted";

        public const string RefusedNotConfigured = "AccessCheck.Refused.NotConfigured";

        public const string RefusedDomainMismatch = "AccessCheck.Refused.DomainMismatch";

        /// <summary>The navigation entry the landing page's settings section gains (spec FR-007).</summary>
        public const string NavigationEntry = "Landing.Settings.AccessCheck";

        public static readonly string[] All =
        [
            Title,
            Explanation,
            RunButton,
            TechnicalAccountLabel,
            VerdictAccessInPlace,
            VerdictNotConfigured,
            VerdictInconclusive,
            StepClassroomRead,
            StepReportsRead,
            OutcomeSucceeded,
            OutcomeScopeNotAuthorized,
            OutcomeTechnicalAccountUnknown,
            OutcomeTechnicalAccountCannotRead,
            OutcomeApiNotEnabled,
            OutcomeKeyUnavailable,
            OutcomeKeyRejected,
            OutcomeGoogleUnavailable,
            OutcomeNotAttempted,
            RefusedNotConfigured,
            RefusedDomainMismatch,
            NavigationEntry,
        ];

        /// <summary>The message key of each outcome (openapi <c>AccessCheckStep.messageKey</c>).</summary>
        public static string Of(AccessCheckStepOutcome outcome) => outcome switch
        {
            AccessCheckStepOutcome.Succeeded => OutcomeSucceeded,
            AccessCheckStepOutcome.ScopeNotAuthorized => OutcomeScopeNotAuthorized,
            AccessCheckStepOutcome.TechnicalAccountUnknown => OutcomeTechnicalAccountUnknown,
            AccessCheckStepOutcome.TechnicalAccountCannotRead => OutcomeTechnicalAccountCannotRead,
            AccessCheckStepOutcome.ApiNotEnabled => OutcomeApiNotEnabled,
            AccessCheckStepOutcome.KeyUnavailable => OutcomeKeyUnavailable,
            AccessCheckStepOutcome.KeyRejected => OutcomeKeyRejected,
            AccessCheckStepOutcome.GoogleUnavailable => OutcomeGoogleUnavailable,
            AccessCheckStepOutcome.NotAttempted => OutcomeNotAttempted,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
    }

    /// <summary>
    /// A text that a Google error answer could carry and that must never reach a page, a log or an audit row
    /// (spec S-08, SC-10). The fake probe cannot return it — the port's contract has no room for it — so its
    /// absence proves nothing about the port; the Infrastructure tests put it into real Google-shaped answers.
    /// </summary>
    public const string GoogleErrorText = "Client is unauthorized to retrieve access tokens using this method";
}
