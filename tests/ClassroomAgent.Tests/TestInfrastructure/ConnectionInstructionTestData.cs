namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The path, the policy name, the fixed scope list, the required absences and the translation keys of the
/// US-010 connection instruction (US-010 openapi; spec FR-004, FR-005, FR-011, FR-012). Every value is fixed
/// by an approved artifact: the path, the policy and the scopes by the openapi, the absences by
/// <c>trebovaniya.md</c> §6, and the key spellings by this stage — TEST_WRITING owns them, as US-009
/// established, because the Specification fixes that keys exist, not how they are spelled.
/// </summary>
public static class ConnectionInstructionTestData
{
    /// <summary>The one path of US-010 openapi. Singular: one instruction per <c>Installation</c> (api-design §2.2).</summary>
    public const string Path = "/settings/connection-instruction";

    /// <summary>The authorization policy of spec FR-011, by name — the constant does not exist yet.</summary>
    public const string Policy = "ViewConnectionInstruction";

    /// <summary>
    /// The id of the element holding the text the Admin hands over (spec FR-008, AC-009). It is a
    /// <c>&lt;pre&gt;</c>: the text is copied verbatim and must keep its line breaks, and a <c>pre</c> cannot
    /// nest, so a test can read exactly what a person would paste.
    /// </summary>
    public const string InstructionElementId = "connection-instruction";

    /// <summary>The id of the copy-to-clipboard control (OD-002).</summary>
    public const string CopyButtonId = "copy-instruction";

    /// <summary>The client ID the seeded <c>legitimacy_state</c> carries — this school's service account.</summary>
    public const string ClientId = InstallationTestData.ClientId;

    /// <summary>A second client ID, for the rotation path (AC-006).</summary>
    public const string RotatedClientId = "300000000000000000003";

    /// <summary>The school's domain, as the last successful check reported it.</summary>
    public const string Domain = InstallationTestData.Domain;

    /// <summary>
    /// Exactly the six scopes of <c>trebovaniya.md</c> §6, in the fixed order of US-010 openapi, as the full
    /// URIs the Google console accepts (spec FR-004, I-4).
    /// </summary>
    public static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/classroom.courses.readonly",
        "https://www.googleapis.com/auth/classroom.rosters.readonly",
        "https://www.googleapis.com/auth/classroom.profile.emails",
        "https://www.googleapis.com/auth/classroom.coursework.students.readonly",
        "https://www.googleapis.com/auth/classroom.courseworkmaterials.readonly",
        "https://www.googleapis.com/auth/admin.reports.audit.readonly",
    ];

    /// <summary>
    /// Scopes that must appear nowhere: the two the prototype requested and no Epic uses (§6, v25), and the
    /// three identity scopes of the Admin's own sign-in — delegation and sign-in are different mechanisms
    /// (§6, v78).
    /// </summary>
    public static readonly string[] ForbiddenScopeFragments =
    [
        "drive.file",
        "classroom.profile.photos",
        "auth/openid",
        "auth/userinfo.email",
        "auth/userinfo.profile",
    ];

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

    public static TheoryData<string> ForbiddenScopes
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var fragment in ForbiddenScopeFragments)
            {
                data.Add(fragment);
            }

            return data;
        }
    }

    /// <summary>
    /// Google Workspace admin-role names the instruction must **not** print (US-054 spec VR-002, I-2). Since
    /// <c>trebovaniya.md</c> v97 (§9, §7 item 10 closed) the only role the instruction names is super-admin; the
    /// lesser delegated roles see no domain courses, so naming one would send a school to a role that does not work.
    /// The list is of role vocabulary, not of one guessed answer: whichever a helpful change reached for, it fails here.
    /// </summary>
    public static readonly string[] ForbiddenRoleNames =
    [
        "Groups Admin",
        "User Management Admin",
        "Help Desk Admin",
        "Services Admin",
        "Reports Admin",
        "Storage Admin",
        "Mobile Admin",
        "Directory Sync",
    ];

    public static TheoryData<string> RoleNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var role in ForbiddenRoleNames)
            {
                data.Add(role);
            }

            return data;
        }
    }

    /// <summary>
    /// The translation keys this Story adds (spec FR-012, AC-010). One key per paragraph or statement, never
    /// one key for the whole instruction (spec I-8), and both language files carry every one.
    /// </summary>
    public static class TextKeys
    {
        public const string Title = "ConnectionInstruction.Title";

        public const string Intro = "ConnectionInstruction.Intro";

        public const string DomainLabel = "ConnectionInstruction.Domain.Label";

        public const string ClientIdLabel = "ConnectionInstruction.ClientId.Label";

        public const string ClientIdThisSchoolOnly = "ConnectionInstruction.ClientId.ThisSchoolOnly";

        public const string NotConfirmed = "ConnectionInstruction.NotConfirmed";

        public const string ScopesLabel = "ConnectionInstruction.Scopes.Label";

        public const string StepAuthoriseDelegation = "ConnectionInstruction.Step.AuthoriseDelegation";

        public const string StepCreateTechnicalAccount = "ConnectionInstruction.Step.CreateTechnicalAccount";

        public const string TechnicalAccountLabel = "ConnectionInstruction.TechnicalAccount.Label";

        public const string TechnicalAccountCreatedBySuperAdmin = "ConnectionInstruction.TechnicalAccount.CreatedBySuperAdmin";

        public const string TechnicalAccountNoPersonBehindIt = "ConnectionInstruction.TechnicalAccount.NoPersonBehindIt";

        /// <summary>US-054 FR-001 #3: the account gets the super-admin role, and why the program still only reads.</summary>
        public const string TechnicalAccountSuperAdminRole = "ConnectionInstruction.TechnicalAccount.SuperAdminRole";

        /// <summary>US-054 FR-001 #4: sign-in under the account is closed by the measures that follow.</summary>
        public const string TechnicalAccountMeasuresLead = "ConnectionInstruction.TechnicalAccount.MeasuresLead";

        public const string MeasureRandomPassword = "ConnectionInstruction.TechnicalAccount.Measure.RandomPassword";

        public const string MeasureTwoStepVerification = "ConnectionInstruction.TechnicalAccount.Measure.TwoStepVerification";

        public const string MeasureSeparateOrgUnit = "ConnectionInstruction.TechnicalAccount.Measure.SeparateOrgUnit";

        public const string MeasureSignInAlert = "ConnectionInstruction.TechnicalAccount.Measure.SignInAlert";

        /// <summary>US-054 FR-001 4.5, OD-002 a: the role is granted last, immediately before the access check.</summary>
        public const string MeasureRoleGrantedLast = "ConnectionInstruction.TechnicalAccount.Measure.RoleGrantedLast";

        public const string TechnicalAccountEnteredInSettings = "ConnectionInstruction.TechnicalAccount.EnteredInSettings";

        public const string CopyButton = "ConnectionInstruction.Copy.Button";

        public const string CopyConfirmation = "ConnectionInstruction.Copy.Confirmation";

        /// <summary>The second entry of the settings section US-009 created (spec FR-013).</summary>
        public const string NavigationEntry = "Landing.Settings.ConnectionInstruction";

        /// <summary>The five measures of US-054 FR-001 4.1–4.5, in order, nested under the lead statement.</summary>
        public static readonly string[] Measures =
        [
            MeasureRandomPassword,
            MeasureTwoStepVerification,
            MeasureSeparateOrgUnit,
            MeasureSignInAlert,
            MeasureRoleGrantedLast,
        ];

        /// <summary>
        /// Every technical-account statement of US-054 FR-001, in the order the instruction makes them: statements 1–4,
        /// the five measures under statement 4, then statement 5.
        /// </summary>
        public static readonly string[] TechnicalAccountStatements =
        [
            TechnicalAccountCreatedBySuperAdmin,
            TechnicalAccountNoPersonBehindIt,
            TechnicalAccountSuperAdminRole,
            TechnicalAccountMeasuresLead,
            .. Measures,
            TechnicalAccountEnteredInSettings,
        ];

        public static readonly string[] All =
        [
            Title,
            Intro,
            DomainLabel,
            ClientIdLabel,
            ClientIdThisSchoolOnly,
            NotConfirmed,
            ScopesLabel,
            StepAuthoriseDelegation,
            StepCreateTechnicalAccount,
            TechnicalAccountLabel,
            TechnicalAccountCreatedBySuperAdmin,
            TechnicalAccountNoPersonBehindIt,
            TechnicalAccountSuperAdminRole,
            TechnicalAccountMeasuresLead,
            .. Measures,
            TechnicalAccountEnteredInSettings,
            CopyButton,
            CopyConfirmation,
            NavigationEntry,
        ];
    }
}
