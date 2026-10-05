using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>
/// US-041 AC-002, AC-003, AC-004 for the Control Plane: an unmatched path answers <c>404</c> to anyone, also when its
/// last segment contains a dot, before and after setup; existing static files and the setup gate are unchanged
/// (spec FR-002 … FR-004, FR-006; OD-001; SC-4 v66).
/// </summary>
public sealed class UnknownFileLikePathTests(PostgreSqlFixture database)
{
    public enum Caller
    {
        Anonymous,
        Owner,
    }

    public static TheoryData<Caller, string> CallersAndPaths()
    {
        var data = new TheoryData<Caller, string>();
        foreach (var caller in new[] { Caller.Anonymous, Caller.Owner })
        {
            foreach (var path in new[] { "/robots.txt", "/css/no-such-file.css", "/favicon.ico", "/no/such/file.json" })
            {
                data.Add(caller, path);
            }
        }

        return data;
    }

    /// <summary>AC-002, FR-002: after setup, the error page with 404 — never a redirect to sign-in.</summary>
    [Theory]
    [MemberData(nameof(CallersAndPaths))]
    public async Task AfterSetup_AMissingFileLikePath_AnswersTheErrorPageWith404(Caller caller, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();
        var client = caller == Caller.Owner ? owner : anonymous;

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Null(page.Location);
        Assert.Contains(host.Text("Error.NotFound", "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-002, FR-002, OD-001: before setup it is 404 too, not the setup gate's 302 /setup.</summary>
    [Theory]
    [InlineData("/robots.txt")]
    [InlineData("/css/no-such-file.css")]
    [InlineData("/favicon.ico")]
    public async Task BeforeSetup_AMissingFileLikePath_Answers404NotTheSetupGate(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Null(page.Location);
        Assert.Contains(host.Text("Error.NotFound", "uk"), page.Text, StringComparison.Ordinal);
        Assert.Equal(0, await host.OwnerCountAsync(ct));
    }

    /// <summary>AC-002, FR-003, spec §7: the catch-all writes nothing — no audit row.</summary>
    [Fact]
    public async Task AMissingFileLikePath_WritesNoAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/robots.txt", ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    /// <summary>AC-003, FR-004: every existing file of <c>wwwroot</c> is still served anonymously with 200, before setup too.</summary>
    [Fact]
    public async Task AnExistingStaticFile_IsStillServedAnonymouslyBeforeSetup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        foreach (var path in NoStore.StaticFilesOf("ClassroomAgent.ControlPlane"))
        {
            var response = await anonymous.GetAsync(path, ct);

            Assert.True(response.Status == HttpStatusCode.OK, $"GET {path}: {(int)response.Status}");
        }
    }

    /// <summary>AC-004, FR-006: before setup a real endpoint is still redirected to /setup.</summary>
    [Fact]
    public async Task BeforeSetup_ARealEndpoint_IsStillRedirectedToSetup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/installations", ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.Equal("/setup", page.Location?.ToString());
    }

    /// <summary>AC-004, FR-006: after setup a real protected endpoint still sends an anonymous caller to sign-in.</summary>
    [Fact]
    public async Task AfterSetup_ARealProtectedEndpoint_StillRedirectsAnAnonymousCaller()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();

        var page = await anonymous.GetAsync("/installations", ct);

        Assert.Equal(HttpStatusCode.Redirect, page.Status);
        Assert.NotNull(page.Location);
        Assert.DoesNotContain("/setup", page.Location!.ToString(), StringComparison.Ordinal);
    }
}
