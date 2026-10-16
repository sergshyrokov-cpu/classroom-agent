using System.Reflection;
using System.Security.Claims;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-052 AC-005 (audit 2 note I-1): the whole <c>trebovaniya.md</c> §2 permission matrix of the installation host
/// in one test class. Two tables (spec I-1): every endpoint → the policy it must declare (FR-009, FR-011, FR-012),
/// and every role policy → its §2 row → the roles that row allows (FR-010). An endpoint added under the wrong
/// policy, a policy that admits the wrong role, an endpoint left to the fallback policy, or a table row with no
/// endpoint fails here, and the message lists every mismatch at once (FR-013).
/// </summary>
public sealed class PermissionMatrixTests(PostgreSqlFixture database)
{
    private const string AnyMethod = "*";

    private static readonly string Admin = InstallationSession.RoleName(AppRole.Admin);

    private static readonly string Dean = InstallationSession.RoleName(AppRole.Dean);

    /// <summary>
    /// The policies of the "any signed-in user" rows (FR-011). They are not §2 cells, so the policy → role table
    /// does not hold them (spec review m-1).
    /// </summary>
    private static readonly string[] SignedInPolicies =
    [
        InstallationPolicies.AuthenticatedUser,
        InstallationPolicies.CompleteTemporaryPasswordChange,
    ];

    /// <summary>FR-010: every installation role policy, its §2 row («Матрица прав», v96) and the roles marked ✔.</summary>
    private static readonly IReadOnlyList<RolePolicy> RolePolicies =
    [
        new(InstallationPolicies.ViewCourses, "Просмотр курсов и участников", [Admin, Dean]),
        new(InstallationPolicies.ViewJournal, "Просмотр и экспорт журнала успеваемости", [Admin, Dean]),
        new(InstallationPolicies.ViewMeetReports, "Просмотр и экспорт статистики Meet-занятий", [Admin, Dean]),
        new(InstallationPolicies.ViewMeetCodes, "Привязка кода встречи Meet к курсу", [Admin, Dean]),
        new(InstallationPolicies.LinkMeetCodes, "Привязка кода встречи Meet к курсу", [Admin, Dean]),
        new(InstallationPolicies.ViewDatabaseStatistics, "Раздел «Управление БД» (статистика)", [Admin, Dean]),
        new(InstallationPolicies.StartSynchronization, "Запуск синхронизации", [Admin, Dean]),
        new(InstallationPolicies.UseReportTemplates, "Использование шаблонов отчётов", [Admin, Dean]),
        new(InstallationPolicies.EditReportTemplates, "Создание и редактирование шаблонов отчётов", [Admin, Dean]),

        // Also covers the v80 row "Просмотр состояния синхронизации с диагностикой ошибок на странице подключения":
        // it is part of the connection page and has no policy of its own (spec FR-010).
        new(InstallationPolicies.ConfigureWorkspaceConnection, "Настройка WorkspaceConnection", [Admin]),
        new(InstallationPolicies.ViewConnectionInstruction, "Просмотр инструкции по подключению", [Admin]),
        new(InstallationPolicies.RunAccessCheck, "«Проверить доступ»", [Admin]),
        new(
            InstallationPolicies.ManageDeanAccounts,
            "Создание, отключение и включение, сброс пароля учётных записей Деканов",
            [Admin]),
        new(
            InstallationPolicies.EraseParticipantData,
            "Удаление всех данных участника по письменному запросу школы и снятие исключения",
            [Admin]),
        new(InstallationPolicies.ChangeOwnPassword, "Смена собственного пароля", [Dean]),
        new(InstallationPolicies.ViewLegitimacyStatus, "Просмотр статуса легитимности инсталляции", [Admin, Dean]),
    ];

    /// <summary>
    /// FR-009: every endpoint of the installation host — method, route pattern as <see cref="HostEndpoint.Pattern"/>
    /// normalises it, and the authorization it must declare. A §2 row names its policy (the §2 row itself is in
    /// <see cref="RolePolicies"/>, spec review m-3); the other two kinds name the requirement that makes them so.
    /// </summary>
    private static readonly IReadOnlyList<MatrixRow> Matrix =
    [
        // SC-4 anonymous list (FR-012; PublicPortAuthorizationTests proves the list itself).
        Anonymous("GET", "sign-in", "SC-4: sign-in page"),
        Anonymous("POST", "sign-in", "SC-4: Dean sign-in form"),
        Anonymous("POST", "sign-in/google", "SC-4: Google OAuth start"),
        Anonymous(AnyMethod, "error/{statuscode}", "SC-4: error page"),
        Anonymous(AnyMethod, "{path}", "SC-4: error page — the fallback catch-all (TC-5)"),
        Anonymous("GET", "health/live", "SC-4, DC-6: liveness, private port"),
        Anonymous("GET", "health/ready", "SC-4, DC-6: readiness, private port"),
        Anonymous("POST", "service/v1/status-pushes", "SC-4, DC-6: status push receiver, private port"),

        // Any signed-in account (FR-011, spec I-2).
        SignedIn("POST", "sign-out", InstallationPolicies.AuthenticatedUser, "§2 BR-026 list, v79: sign-out"),
        SignedIn("POST", "account/language", InstallationPolicies.AuthenticatedUser, "v51, NFR-073: UI language"),
        SignedIn(
            "GET",
            "sign-in/change-password",
            InstallationPolicies.AuthenticatedUser,
            "§2 v38/v64: forced temporary-password change page"),
        SignedIn(
            "POST",
            "sign-in/change-password",
            InstallationPolicies.CompleteTemporaryPasswordChange,
            "§2 v38/v64: forced temporary-password change form"),

        // §2 cells.
        Cell("GET", "", InstallationPolicies.ViewLegitimacyStatus),

        Cell("GET", "settings/workspace-connection", InstallationPolicies.ConfigureWorkspaceConnection),
        Cell("POST", "settings/workspace-connection", InstallationPolicies.ConfigureWorkspaceConnection),
        Cell("GET", "settings/connection-instruction", InstallationPolicies.ViewConnectionInstruction),
        Cell("GET", "settings/access-check", InstallationPolicies.RunAccessCheck),
        Cell("POST", "settings/access-check", InstallationPolicies.RunAccessCheck),

        Cell("POST", "synchronization/requests", InstallationPolicies.StartSynchronization),
        Cell("GET", "database", InstallationPolicies.ViewDatabaseStatistics),
        Cell("POST", "database/synchronization-requests", InstallationPolicies.StartSynchronization),

        Cell("GET", "workspace/journal", InstallationPolicies.ViewJournal),

        // Audit 2 I-2, spec I-3: the journal export sits under UseReportTemplates; both rows are ✔/✔.
        Cell("POST", "api/v1/exports/journal-xlsx", InstallationPolicies.UseReportTemplates),

        Cell("GET", "workspace/courses", InstallationPolicies.ViewCourses),
        Cell("GET", "workspace/courses/{courseid}", InstallationPolicies.ViewCourses),
        Cell("GET", "workspace/teachers", InstallationPolicies.ViewCourses),

        Cell("GET", "workspace/meet-reports", InstallationPolicies.ViewMeetReports),
        Cell("GET", "workspace/meet-reports/meetings/{meetingid}", InstallationPolicies.ViewMeetReports),
        Cell("POST", "api/v1/exports/meet-reports-xlsx", InstallationPolicies.ViewMeetReports),
        Cell("POST", "api/v1/exports/meet-meeting-xlsx", InstallationPolicies.ViewMeetReports),

        Cell("GET", "workspace/meet-codes", InstallationPolicies.ViewMeetCodes),
        Cell("GET", "workspace/meet-codes/{meetingcode}/course-choice", InstallationPolicies.LinkMeetCodes),
        Cell("POST", "workspace/meet-codes/{meetingcode}/link", InstallationPolicies.LinkMeetCodes),
        Cell("POST", "workspace/meet-codes/{meetingcode}/confirmation", InstallationPolicies.LinkMeetCodes),
        Cell("POST", "workspace/meet-codes/{meetingcode}/not-a-course-mark", InstallationPolicies.LinkMeetCodes),

        Cell("GET", "reports", InstallationPolicies.UseReportTemplates),
        Cell("GET", "reports/templates", InstallationPolicies.UseReportTemplates),
        Cell("GET", "reports/templates/new", InstallationPolicies.EditReportTemplates),
        Cell("GET", "reports/templates/{templateref}/copy", InstallationPolicies.EditReportTemplates),
        Cell("GET", "reports/templates/{templateref}/edit", InstallationPolicies.EditReportTemplates),
        Cell("GET", "reports/templates/{templateref}/deletion", InstallationPolicies.EditReportTemplates),
        Cell("POST", "reports/templates", InstallationPolicies.EditReportTemplates),
        Cell("POST", "reports/templates/{templateref}", InstallationPolicies.EditReportTemplates),
        Cell("POST", "reports/templates/{templateref}/deletion", InstallationPolicies.EditReportTemplates),

        Cell("GET", "settings/deans", InstallationPolicies.ManageDeanAccounts),
        Cell("POST", "settings/deans", InstallationPolicies.ManageDeanAccounts),
        Cell("POST", "settings/deans/{deanid}/state", InstallationPolicies.ManageDeanAccounts),
        Cell("POST", "settings/deans/{deanid}/password", InstallationPolicies.ManageDeanAccounts),

        Cell("GET", "account/password", InstallationPolicies.ChangeOwnPassword),
        Cell("POST", "account/password", InstallationPolicies.ChangeOwnPassword),

        Cell("GET", "settings/erasure", InstallationPolicies.EraseParticipantData),
        Cell("POST", "settings/erasure/searches", InstallationPolicies.EraseParticipantData),
        Cell("GET", "settings/erasure/participants/{participantid}", InstallationPolicies.EraseParticipantData),
        Cell("POST", "settings/erasure/participants/{participantid}/erasure", InstallationPolicies.EraseParticipantData),
        Cell("POST", "settings/erasure/exclusion-liftings", InstallationPolicies.EraseParticipantData),
    ];

    /// <summary>
    /// AC-005, FR-012, FR-013: every endpoint the host exposes is a matrix row with the authorization of that row,
    /// and every matrix row is an endpoint. All mismatches are reported together.
    /// </summary>
    [Fact]
    public async Task EveryEndpoint_DeclaresThePolicyOfItsMatrixRow_AndEveryRowIsAnEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var actual = HostEndpoint.All(host.Services)

            // FR-012: static files are served by the static-files middleware; an endpoint of that shape would be the
            // SC-4 static entry and is not a matrix row.
            .Where(e => !(e.IsStaticFile && e.AllowsAnonymous))
            .SelectMany(e => (e.Methods ?? [AnyMethod]).Select(m => (Key: Key(m, e.Pattern), Endpoint: e)))
            .ToList();
        var expected = Matrix.ToDictionary(r => Key(r.Method, r.Pattern));
        var mismatches = new List<string>();

        foreach (var duplicate in actual.GroupBy(a => a.Key).Where(g => g.Count() > 1))
        {
            mismatches.Add($"{duplicate.Key}: {duplicate.Count()} endpoints share this method and pattern");
        }

        foreach (var (key, endpoint) in actual)
        {
            var found = Describe(endpoint);
            if (!expected.TryGetValue(key, out var row))
            {
                mismatches.Add($"{key}: not in the matrix table; declares {found}");
            }
            else if (found != row.Expected)
            {
                mismatches.Add($"{key}: expected {row.Expected} ({row.Basis}); declares {found}");
            }
        }

        var exposed = actual.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        mismatches.AddRange(Matrix
            .Where(r => !exposed.Contains(Key(r.Method, r.Pattern)))
            .Select(r => $"{Key(r.Method, r.Pattern)}: a matrix row with no endpoint (expected {r.Expected})"));

        Assert.True(mismatches.Count == 0, "Permission matrix drift:\n" + string.Join("\n", mismatches));
    }

    /// <summary>
    /// FR-010, spec review m-1: every role policy the installation defines is in the policy → §2 row table and
    /// vice versa; the two signed-in policies are exempt, being no §2 cell.
    /// </summary>
    [Fact]
    public void ThePolicyTable_HoldsEveryRolePolicy_AndNothingElse()
    {
        var defined = typeof(InstallationPolicies)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Except(SignedInPolicies)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(defined, RolePolicies.Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// FR-010: each policy of the table is registered in the host and admits exactly the roles its §2 row marks ✔ —
    /// no more, no fewer — and nobody who is not signed in. All mismatches are reported together (FR-013).
    /// </summary>
    [Fact]
    public async Task EveryRolePolicy_AdmitsExactlyTheRolesOfItsMatrixRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var provider = host.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = host.Services.GetRequiredService<IAuthorizationService>();
        var mismatches = new List<string>();

        foreach (var policy in RolePolicies)
        {
            if (await provider.GetPolicyAsync(policy.Name) is null)
            {
                mismatches.Add($"{policy.Name}: not registered in the host");
                continue;
            }

            var admitted = new List<string>();
            foreach (var role in new[] { Admin, Dean })
            {
                if ((await authorization.AuthorizeAsync(SignedInAs(role), resource: null, policy.Name)).Succeeded)
                {
                    admitted.Add(role);
                }
            }

            if (!admitted.Order(StringComparer.Ordinal).SequenceEqual(policy.Roles.Order(StringComparer.Ordinal)))
            {
                mismatches.Add(
                    $"{policy.Name} («{policy.Row}»): §2 allows [{string.Join(", ", policy.Roles)}]; "
                    + $"the policy admits [{string.Join(", ", admitted)}]");
            }

            if ((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), resource: null, policy.Name)).Succeeded)
            {
                mismatches.Add($"{policy.Name}: admits a visitor who is not signed in");
            }
        }

        Assert.True(mismatches.Count == 0, "Role policy drift:\n" + string.Join("\n", mismatches));
    }

    private static ClaimsPrincipal SignedInAs(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "1")],
            CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>What an endpoint declares, in the form <see cref="MatrixRow.Expected"/> takes.</summary>
    private static string Describe(HostEndpoint endpoint)
    {
        if (endpoint.AllowsAnonymous)
        {
            return "anonymous";
        }

        var policies = endpoint.Endpoint.Metadata
            .OfType<IAuthorizeData>()
            .Select(a => a.Policy)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        // SC-4: every endpoint declares its policy; one served by the fallback policy alone is a defect.
        return policies.Count == 0 ? "no declared policy (fallback only)" : "policy " + string.Join("+", policies);
    }

    private static string Key(string method, string pattern) => $"{method.ToUpperInvariant()} /{pattern}";

    private static MatrixRow Anonymous(string method, string pattern, string basis) =>
        new(method, pattern, "anonymous", basis);

    private static MatrixRow SignedIn(string method, string pattern, string policy, string basis) =>
        new(method, pattern, "policy " + policy, "any signed-in user — " + basis);

    private static MatrixRow Cell(string method, string pattern, string policy) =>
        new(method, pattern, "policy " + policy, "§2 «" + RolePolicies.Single(p => p.Name == policy).Row + "»");

    private sealed record RolePolicy(string Name, string Row, IReadOnlyList<string> Roles);

    private sealed record MatrixRow(string Method, string Pattern, string Expected, string Basis);
}
