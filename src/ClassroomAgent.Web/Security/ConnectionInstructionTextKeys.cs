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

    /// <summary>BR-015: no person behind it, not a super-admin, nobody signs in to the program with it.</summary>
    public const string TechnicalAccountNoPersonBehindIt = "ConnectionInstruction.TechnicalAccount.NoPersonBehindIt";

    /// <summary>
    /// BR-015, OD-001: it must be able to **read** Classroom and Admin Reports and hold nothing beyond that.
    /// The requirement, not a role list — no Workspace admin-role name is printed while
    /// <c>trebovaniya.md</c> §7 item 10 is unverified.
    /// </summary>
    public const string TechnicalAccountReadOnlyRoles = "ConnectionInstruction.TechnicalAccount.ReadOnlyRoles";

    /// <summary>§9, BR-030: a super-admin account, and any account with write access, are not acceptable.</summary>
    public const string TechnicalAccountNoWriteAccess = "ConnectionInstruction.TechnicalAccount.NoWriteAccess";

    /// <summary>BR-020: its address is entered by the Admin in the connection settings, in the school's domain.</summary>
    public const string TechnicalAccountEnteredInSettings = "ConnectionInstruction.TechnicalAccount.EnteredInSettings";

    public const string CopyButton = "ConnectionInstruction.Copy.Button";

    public const string CopyConfirmation = "ConnectionInstruction.Copy.Confirmation";

    /// <summary>The second entry of the settings section US-009 created (spec FR-013).</summary>
    public const string NavigationEntry = "Landing.Settings.ConnectionInstruction";

    /// <summary>The five statements of spec FR-005, in the order the instruction makes them.</summary>
    public static readonly string[] TechnicalAccountStatements =
    [
        TechnicalAccountCreatedBySuperAdmin,
        TechnicalAccountNoPersonBehindIt,
        TechnicalAccountReadOnlyRoles,
        TechnicalAccountNoWriteAccess,
        TechnicalAccountEnteredInSettings,
    ];
}
