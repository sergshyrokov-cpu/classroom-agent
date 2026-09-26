using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-012 AC-014 — the criterion this Story exists to make provable. Until now no Dean could sign in, so the
/// forbidden-role tests of US-009, US-010 and US-011 built a principal by hand and asked the policy directly
/// (US-010 F-1, carried to US-011 F-1 with owner US-012). Here a **real** Dean session asks the three
/// Admin-only settings pages over HTTP and is refused by the host, not by a policy object (TC-5).
/// </summary>
public sealed class DeanSessionBoundaryTests(PostgreSqlFixture database)
{
    /// <summary>The three Admin-only settings paths of US-009, US-010 and US-011.</summary>
    public static TheoryData<string> AdminOnlyPaths => new(
        WorkspaceConnectionTestData.Path,
        ConnectionInstructionTestData.Path,
        AccessCheckTestData.Path);

    /// <summary>AC-014: a signed-in Dean gets 403 from each Admin-only settings page, on GET.</summary>
    [Theory]
    [MemberData(nameof(AdminOnlyPaths))]
    public async Task ASignedInDean_IsForbiddenFromTheAdminOnlySettingsPages(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = await SignInAsDeanAsync(host, ct);

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.Forbidden, page.Status);
    }

    /// <summary>
    /// AC-014: and on POST, for the two pages that accept one — a forbidden role must not be able to act
    /// through a method the page happens to expose.
    /// </summary>
    [Theory]
    [InlineData(WorkspaceConnectionTestData.Path)]
    [InlineData(AccessCheckTestData.Path)]
    public async Task ASignedInDean_IsForbiddenFromPostingToThem(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = await SignInAsDeanAsync(host, ct);
        await client.GetAsync(DeanAccountTestData.Paths.Landing, ct);

        var page = await client.PostFormAsync(path, [], ct);

        Assert.Equal(HttpStatusCode.Forbidden, page.Status);
    }

    /// <summary>AC-014: the Dean is also forbidden from the Dean accounts screen itself (spec FR-016).</summary>
    [Fact]
    public async Task ASignedInDean_IsForbiddenFromTheDeanAccountsScreen()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = await SignInAsDeanAsync(host, ct);

        var page = await client.OpenDeanAccountsAsync(ct);

        Assert.Equal(HttpStatusCode.Forbidden, page.Status);
    }

    /// <summary>
    /// AC-014: the same Dean keeps 200 on what both roles may see, so the test proves a role boundary and not a
    /// broken session (spec FR-016).
    /// </summary>
    [Fact]
    public async Task TheSameDean_StillSeesWhatBothRolesMaySee()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var client = await SignInAsDeanAsync(host, ct);

        var page = await client.GetAsync(DeanAccountTestData.Paths.Landing, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
    }

    /// <summary>A Dean whose password is not temporary, signed in by password through the real flow.</summary>
    private static async Task<FormClient> SignInAsDeanAsync(InstallationTestHost host, CancellationToken cancellationToken)
    {
        await host.InsertDeanAsync(cancellationToken);
        var client = host.CreateClient();
        await client.SignInAsDeanAsync(
            DeanAccountTestData.DeanEmail,
            DeanAccountTestData.TemporaryPassword,
            cancellationToken);
        await client.CompleteForcedChangeAsync(DeanAccountTestData.NewPassword, cancellationToken);
        return client;
    }
}
