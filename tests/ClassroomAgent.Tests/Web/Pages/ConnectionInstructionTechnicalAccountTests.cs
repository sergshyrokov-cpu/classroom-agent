using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-010 AC-004: what the instruction says about the school's technical account — the five statements of spec
/// FR-005 (BR-015, <c>trebovaniya.md</c> §9) — and what it must **not** say: no Google Workspace admin-role name
/// while §7 item 10 is unverified (OD-001, spec VR-005).
/// </summary>
public sealed class ConnectionInstructionTechnicalAccountTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Statements
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var key in ConnectionInstructionTestData.TextKeys.TechnicalAccountStatements)
            {
                data.Add(key);
            }

            return data;
        }
    }

    /// <summary>AC-004: each of the five statements is on the page, in the reader's language.</summary>
    [Theory]
    [MemberData(nameof(Statements))]
    public async Task EveryTechnicalAccountStatement_IsOnThePage(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(host.Text(key, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-004, OD-001: **no Workspace admin-role name is printed**, in either language, while
    /// <c>trebovaniya.md</c> §7 item 10 is unverified. This is the guard the resolution asked for: a later
    /// "helpful" addition to a translation file fails here instead of reaching a school.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConnectionInstructionTestData.RoleNames), MemberType = typeof(ConnectionInstructionTestData))]
    public async Task NoWorkspaceAdminRoleName_AppearsInEitherLanguage(string roleName)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        string[] cultures = ["uk", "en"];

        foreach (var culture in cultures)
        {
            foreach (var key in ConnectionInstructionTestData.TextKeys.All)
            {
                Assert.DoesNotContain(roleName, host.Text(key, culture), StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>AC-004, OD-001: nor does one reach the rendered page.</summary>
    [Theory]
    [MemberData(nameof(ConnectionInstructionTestData.RoleNames), MemberType = typeof(ConnectionInstructionTestData))]
    public async Task NoWorkspaceAdminRoleName_ReachesThePage(string roleName)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(System.Net.HttpStatusCode.OK, page.Status);
        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.TechnicalAccountReadOnlyRoles, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(roleName, page.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-004, §9: the instruction states the two actions the school's super-admin performs — authorise
    /// domain-wide delegation for this client ID with these scopes, and create the technical account.
    /// </summary>
    [Fact]
    public async Task ThePage_StatesWhatTheSuperAdminDoes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.StepAuthoriseDelegation, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.StepCreateTechnicalAccount, "uk"),
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-004, spec FR-005 statement 4: the instruction never suggests that a super-admin account, or any
    /// account with write access, would do. Both statements exist as their own keys, so each is testable.
    /// </summary>
    [Fact]
    public async Task ThePage_SaysASuperAdminAccountIsNotAcceptable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.TechnicalAccountNoPersonBehindIt, "uk"),
            page.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.TechnicalAccountNoWriteAccess, "uk"),
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-004: the instruction does not put a technical-account address in front of the super-admin. The address
    /// is what the Admin enters afterwards in the connection settings (US-009), and this page never invents one.
    /// </summary>
    [Fact]
    public async Task ThePage_DoesNotInventATechnicalAccountAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        await host.InsertWorkspaceConnectionAsync(ct, impersonationUserEmail: WorkspaceConnectionTestData.TechnicalAccount);

        var page = await client.OpenInstructionAsync(ct);

        Assert.DoesNotContain(WorkspaceConnectionTestData.TechnicalAccount, page.Text, StringComparison.Ordinal);
        Assert.Contains(
            host.Text(ConnectionInstructionTestData.TextKeys.TechnicalAccountEnteredInSettings, "uk"),
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-004, spec S-12: the page shows no personal data at all — not a student's, not a Dean's, and not even
    /// the signed-in Admin's own address, which the landing page shows and this page has no reason to.
    /// </summary>
    [Fact]
    public async Task ThePage_ShowsNoPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await ConnectionInstructionHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;

        var page = await client.OpenInstructionAsync(ct);

        Assert.Equal(System.Net.HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain(SignInTestData.AdminEmail, page.Text, StringComparison.OrdinalIgnoreCase);
    }
}
