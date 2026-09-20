namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Paths, the single form field, translation keys and synthetic addresses of the connection settings
/// (US-009 openapi; spec FR-004, FR-005, FR-011). Every value is fixed by an approved artifact; a test that
/// needs a different one is testing something the contract does not say.
/// </summary>
public static class WorkspaceConnectionTestData
{
    /// <summary>The one path of US-009 openapi, serving both the page and the save.</summary>
    public const string Path = "/settings/workspace-connection";

    /// <summary>The only field of the save request (openapi <c>SaveWorkspaceConnectionForm</c>, spec VR-003).</summary>
    public const string EmailField = "impersonationUserEmail";

    /// <summary>The domain the seeded <c>legitimacy_state</c> carries — the domain the Owner approved.</summary>
    public const string AllowedDomain = InstallationTestData.Domain;

    /// <summary>The school's technical account: no person behind it, read-only roles only (BR-015).</summary>
    public const string TechnicalAccount = "classroom-agent@school-one.example.test";

    /// <summary>A second technical account, for the change path (AC-007).</summary>
    public const string OtherTechnicalAccount = "sync-agent@school-one.example.test";

    /// <summary>The same address in mixed case and padded — normalisation must make it the same account (AC-003).</summary>
    public const string TechnicalAccountMixedCase = "  Classroom-Agent@School-One.Example.Test  ";

    /// <summary>An address in a neighbouring school's domain (AC-005).</summary>
    public const string NeighbouringSchoolAccount = "classroom-agent@school-two.example.test";

    /// <summary>A personal address outside any school domain (AC-005).</summary>
    public const string PersonalAccount = "someone@gmail.example.test";

    /// <summary>A subdomain of the school's domain: not the domain (AC-005, spec FR-007).</summary>
    public const string SubdomainAccount = "classroom-agent@sync.school-one.example.test";

    /// <summary>The Admin's own account — the technical account is never a person's (BR-015).</summary>
    public const string AdminOwnAccount = SignInTestData.AdminEmail;

    /// <summary>Longer than the 254 characters VR-001 allows.</summary>
    public static string TooLongAccount { get; } = new string('a', 250) + "@" + AllowedDomain;

    /// <summary>Addresses VR-001 rejects before any business rule runs (AC-006).</summary>
    public static TheoryData<string> MalformedAddresses => new(
        string.Empty,
        "   ",
        "no-at-sign.school-one.example.test",
        "two@at@school-one.example.test",
        "@school-one.example.test",
        "classroom-agent@",
        "classroom agent@school-one.example.test",
        "classroom-agent@school one.example.test",
        "classroom-agent@school-one",
        "classroom-agent@-school-one.example.test",
        "classroom-agent@school-one.example.test-");

    /// <summary>Addresses whose domain is not the installation's, each well formed (AC-005).</summary>
    public static TheoryData<string> ForeignDomainAddresses => new(
        NeighbouringSchoolAccount,
        PersonalAccount,
        SubdomainAccount);

    /// <summary>Spellings of the allowed domain that must be accepted as the same domain (AC-004, spec FR-007).</summary>
    public static TheoryData<string> EquivalentSpellings => new(
        "classroom-agent@" + AllowedDomain,
        "classroom-agent@" + AllowedDomain.ToUpperInvariant(),
        "classroom-agent@" + AllowedDomain + ".",
        "  classroom-agent@" + AllowedDomain + "  ");

    /// <summary>The audit values US-009 db-design §4 fixes.</summary>
    public static class Audit
    {
        public const string Action = "workspace_connection_saved";

        public const string TargetType = "workspace_connection";

        public const string DomainMismatch = "domain_mismatch";

        public const string ImpersonationDomainMismatch = "impersonation_domain_mismatch";

        public const string DomainNotConfirmed = "domain_not_confirmed";

        public const string ReadOnlyMode = "read_only_mode";
    }

    /// <summary>The authorization policy of spec FR-010, by name — the constant does not exist yet.</summary>
    public const string Policy = "ConfigureWorkspaceConnection";

    /// <summary>
    /// The translation keys this Story adds (spec FR-011, AC-011). The implementation must use exactly these:
    /// a key is part of the contract between the view and the translation files, and both files carry every one.
    /// </summary>
    public static class TextKeys
    {
        public const string Title = "WorkspaceConnection.Title";

        public const string DomainLabel = "WorkspaceConnection.Domain.Label";

        public const string DomainUnknown = "WorkspaceConnection.Domain.Unknown";

        public const string ImpersonationUserLabel = "WorkspaceConnection.ImpersonationUser.Label";

        public const string TechnicalAccountHint = "WorkspaceConnection.ImpersonationUser.Hint";

        public const string CheckAccessHint = "WorkspaceConnection.CheckAccess.Hint";

        public const string StateNotConfigured = "WorkspaceConnection.State.NotConfigured";

        public const string StateConfigured = "WorkspaceConnection.State.Configured";

        public const string StateDomainMismatch = "WorkspaceConnection.State.DomainMismatch";

        public const string SaveButton = "WorkspaceConnection.Save.Button";

        public const string Saved = "WorkspaceConnection.Saved";

        public const string RefusedDomainMismatch = "WorkspaceConnection.Refused.DomainMismatch";

        public const string RefusedImpersonationDomainMismatch = "WorkspaceConnection.Refused.ImpersonationDomainMismatch";

        public const string RefusedDomainNotConfirmed = "WorkspaceConnection.Refused.DomainNotConfirmed";

        public const string ValidationRequired = "WorkspaceConnection.Validation.Required";

        public const string ValidationEmail = "WorkspaceConnection.Validation.Email";

        public const string ValidationDomain = "WorkspaceConnection.Validation.Domain";

        public const string ValidationTooLong = "WorkspaceConnection.Validation.TooLong";

        /// <summary>The navigation entry the landing page gains (spec FR-013).</summary>
        public const string NavigationEntry = "Landing.Settings.WorkspaceConnection";

        public static readonly string[] All =
        [
            Title,
            DomainLabel,
            DomainUnknown,
            ImpersonationUserLabel,
            TechnicalAccountHint,
            CheckAccessHint,
            StateNotConfigured,
            StateConfigured,
            StateDomainMismatch,
            SaveButton,
            Saved,
            RefusedDomainMismatch,
            RefusedImpersonationDomainMismatch,
            RefusedDomainNotConfirmed,
            ValidationRequired,
            ValidationEmail,
            ValidationDomain,
            ValidationTooLong,
            NavigationEntry,
        ];
    }
}
