namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-054: the texts that describe the school's technical account as <c>trebovaniya.md</c> §9 v97 does — a
/// super-admin whose sign-in is closed by mandatory measures. <see cref="Binding"/> is the binding wording of spec
/// §4.1, held as literals so a test fails on any drift from the approved text; <see cref="Retired"/> is what those
/// entries said before v97, held as literals so a test can prove it reaches no page any more (spec I-5).
/// </summary>
public static class TechnicalAccountTextsTestData
{
    public const string HintKey = "WorkspaceConnection.ImpersonationUser.Hint";

    public const string CannotReadKey = "AccessCheck.Outcome.TechnicalAccountCannotRead";

    /// <summary>The binding uk and en values of spec §4.1, by key.</summary>
    public static readonly IReadOnlyDictionary<string, (string Uk, string En)> Binding =
        new Dictionary<string, (string Uk, string En)>(StringComparer.Ordinal)
        {
            [ConnectionInstructionTestData.TextKeys.TechnicalAccountNoPersonBehindIt] = (
                "за ним не стоїть людина, і в програму під ним ніхто не входить;",
                "no person stands behind it, and nobody signs in to the program with it;"),
            [ConnectionInstructionTestData.TextKeys.TechnicalAccountSuperAdminRole] = (
                "він отримує роль super-admin: з меншими ролями Classroom курсів домену не видно, а програма однаково лише читає, бо її доступ обмежують scope у domain-wide delegation, а не роль акаунта;",
                "it gets the super-admin role: lesser Classroom roles do not see the domain's courses, and the program still only reads, because its access is bounded by the scopes in domain-wide delegation, not by the account's role;"),
            [ConnectionInstructionTestData.TextKeys.TechnicalAccountMeasuresLead] = (
                "вхід під ним закривають обов'язкові заходи:",
                "sign-in under it is closed by these mandatory measures:"),
            [ConnectionInstructionTestData.TextKeys.MeasureRandomPassword] = (
                "випадковий пароль, який ніде не зберігається;",
                "a random password that is stored nowhere;"),
            [ConnectionInstructionTestData.TextKeys.MeasureTwoStepVerification] = (
                "примусова двоетапна перевірка без відстрочки реєстрації і без жодного зареєстрованого способу підтвердження — без резервних кодів, резервної пошти й телефону, тож увійти під акаунтом неможливо;",
                "enforced 2-Step Verification with no enrolment grace period and no method enrolled — no backup codes, no recovery email, no recovery phone — so that signing in to the account is impossible;"),
            [ConnectionInstructionTestData.TextKeys.MeasureSeparateOrgUnit] = (
                "окремий організаційний підрозділ, у якому діють ці правила;",
                "a separate organizational unit that carries these settings;"),
            [ConnectionInstructionTestData.TextKeys.MeasureSignInAlert] = (
                "правило сповіщень про будь-який вхід в акаунт; лист отримує адміністратор домену;",
                "an alert rule for any sign-in to the account, emailed to a domain administrator;"),
            [ConnectionInstructionTestData.TextKeys.MeasureRoleGrantedLast] = (
                "роль super-admin видається останньою — після всіх інших заходів, безпосередньо перед тим, як Адміністратор натисне «Перевірити доступ», і не раніше;",
                "the super-admin role is granted last — after all the other measures, immediately before an Administrator clicks \"Check access\", and not earlier;"),
            [HintKey] = (
                "Технічний акаунт створює super-admin школи. Це не ваш акаунт: за ним не стоїть людина, і вхід під ним заблоковано. Він має роль super-admin, але програма лише читає — її доступ обмежують scope у domain-wide delegation.",
                "The technical account is created by the school's super-admin. It is not your own account: no person stands behind it, and sign-in under it is blocked. It holds the super-admin role, but the program only reads — its access is bounded by the scopes in domain-wide delegation."),
            [CannotReadKey] = (
                "доступ дозволено, але технічний обліковий запис не має прав на це читання. Super-admin школи має надати йому роль super-admin, як сказано в інструкції з підключення.",
                "access is authorised, but the technical account has no right to this read. The school's super-admin must give it the super-admin role, as the connection instructions say."),
        };

    /// <summary>The two instruction keys spec FR-002 removes from both files.</summary>
    public static readonly string[] RemovedKeys =
    [
        "ConnectionInstruction.TechnicalAccount.ReadOnlyRoles",
        "ConnectionInstruction.TechnicalAccount.NoWriteAccess",
    ];

    /// <summary>
    /// The pre-v97 values of the instruction statements and the connection-page hint (spec I-5), as they were before
    /// this Story, in uk and en. None may reach the instruction or the connection page again (AC-003).
    /// </summary>
    public static readonly string[] Retired =
    [
        "за ним не стоїть людина, він не super-admin, і в програму під ним ніхто не входить;",
        "no person stands behind it, it is not a super-admin, and nobody signs in to the program with it;",
        "він має читати Classroom і Admin Reports — і не мати нічого понад це;",
        "it must be able to read Classroom and Admin Reports — and hold nothing beyond that;",
        "акаунт super-admin і будь-який акаунт із правом запису не підходять: програма нічого не записує в Google Workspace;",
        "a super-admin account, and any account with write access, are not acceptable: the program writes nothing to Google Workspace;",
        "Технічний акаунт створює super-admin школи. Це не ваш акаунт і не super-admin: за ним не стоїть людина, і він має лише права на читання.",
        "The technical account is created by the school's super-admin. It is neither your own account nor a super-admin: no person stands behind it, and it holds read-only roles.",
    ];

    /// <summary>The pre-v97 advice of the "cannot read" diagnosis (AC-007: not read-only rights), uk and en.</summary>
    public static readonly string[] RetiredCannotReadAdvice =
    [
        "Super-admin школи має надати йому права лише на читання цих даних.",
        "The school's super-admin must give it read-only rights to this data.",
    ];

    public static string Wording(string key, string language) =>
        language == "en" ? Binding[key].En : Binding[key].Uk;

    public static TheoryData<string, string> BindingByLanguage
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var key in Binding.Keys)
            {
                data.Add(key, "uk");
                data.Add(key, "en");
            }

            return data;
        }
    }

    public static TheoryData<string> RetiredValues
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var value in Retired)
            {
                data.Add(value);
            }

            return data;
        }
    }
}
