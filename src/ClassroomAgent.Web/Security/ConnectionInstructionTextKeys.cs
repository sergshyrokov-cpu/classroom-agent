namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the super-admin instruction (US-010 spec FR-012). **One key per paragraph or
/// statement**, never one key for the whole instruction, so a change to one sentence does not invalidate both
/// translations wholesale (spec I-8). Named once, so the view, the controller and the tests cannot drift; every
/// key exists in both the Ukrainian and the English file.
/// </summary>
public static class ConnectionInstructionTextKeys
{
    public const string Title = "ConnectionInstruction.Title";

    public const string Intro = "ConnectionInstruction.Intro";

    public const string DomainLabel = "ConnectionInstruction.Domain.Label";

    public const string ClientIdLabel = "ConnectionInstruction.ClientId.Label";

    public const string ClientIdThisSchoolOnly = "ConnectionInstruction.ClientId.ThisSchoolOnly";

    /// <summary>Replaces the domain and the client ID while the installation has not confirmed its legitimacy.</summary>
    public const string NotConfirmed = "ConnectionInstruction.NotConfirmed";

    public const string ScopesLabel = "ConnectionInstruction.Scopes.Label";

    public const string StepAuthoriseDelegation = "ConnectionInstruction.Step.AuthoriseDelegation";

    public const string StepCreateTechnicalAccount = "ConnectionInstruction.Step.CreateTechnicalAccount";

    public const string TechnicalAccountLabel = "ConnectionInstruction.TechnicalAccount.Label";

    /// <summary>BR-015: the school's super-admin creates the account at onboarding, with the delegation.</summary>
    public const string TechnicalAccountCreatedBySuperAdmin = "ConnectionInstruction.TechnicalAccount.CreatedBySuperAdmin";

    /// <summary>BR-015: no person stands behind it, and nobody signs in to the program with it.</summary>
    public const string TechnicalAccountNoPersonBehindIt = "ConnectionInstruction.TechnicalAccount.NoPersonBehindIt";

    /// <summary>
    /// §9 v97: it gets the super-admin role, because lesser Classroom roles do not see the domain's courses; the
    /// program still only reads, bounded by the delegation scopes, not by the account's role.
    /// </summary>
    public const string TechnicalAccountSuperAdminRole = "ConnectionInstruction.TechnicalAccount.SuperAdminRole";

    /// <summary>§9 v97: sign-in under the account is closed by the mandatory measures that follow.</summary>
    public const string TechnicalAccountMeasuresLead = "ConnectionInstruction.TechnicalAccount.MeasuresLead";

    public const string MeasureRandomPassword = "ConnectionInstruction.TechnicalAccount.Measure.RandomPassword";

    public const string MeasureTwoStepVerification = "ConnectionInstruction.TechnicalAccount.Measure.TwoStepVerification";

    public const string MeasureSeparateOrgUnit = "ConnectionInstruction.TechnicalAccount.Measure.SeparateOrgUnit";

    public const string MeasureSignInAlert = "ConnectionInstruction.TechnicalAccount.Measure.SignInAlert";

    /// <summary>§9 v97: the super-admin role is granted last, immediately before the access check.</summary>
    public const string MeasureRoleGrantedLast = "ConnectionInstruction.TechnicalAccount.Measure.RoleGrantedLast";

    /// <summary>BR-020: its address is entered by the Admin in the connection settings, in the school's domain.</summary>
    public const string TechnicalAccountEnteredInSettings = "ConnectionInstruction.TechnicalAccount.EnteredInSettings";

    public const string CopyButton = "ConnectionInstruction.Copy.Button";

    public const string CopyConfirmation = "ConnectionInstruction.Copy.Confirmation";

    /// <summary>The second entry of the settings section US-009 created (spec FR-013).</summary>
    public const string NavigationEntry = "Landing.Settings.ConnectionInstruction";

    /// <summary>
    /// The five top-level technical-account statements of US-054 FR-001, in the order the instruction makes them;
    /// <see cref="TechnicalAccountMeasures"/> are nested under <see cref="TechnicalAccountMeasuresLead"/>.
    /// </summary>
    public static readonly string[] TechnicalAccountStatements =
    [
        TechnicalAccountCreatedBySuperAdmin,
        TechnicalAccountNoPersonBehindIt,
        TechnicalAccountSuperAdminRole,
        TechnicalAccountMeasuresLead,
        TechnicalAccountEnteredInSettings,
    ];

    /// <summary>The five mandatory measures of US-054 FR-001 4.1–4.5, in order (§9 v97).</summary>
    public static readonly string[] TechnicalAccountMeasures =
    [
        MeasureRandomPassword,
        MeasureTwoStepVerification,
        MeasureSeparateOrgUnit,
        MeasureSignInAlert,
        MeasureRoleGrantedLast,
    ];
}
