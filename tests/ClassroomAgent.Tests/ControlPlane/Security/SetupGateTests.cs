using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-001: while no Owner account exists every page leads to setup (FR-001).</summary>
public sealed class SetupGateTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Home_WithoutOwner_RedirectsToSetup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/setup", response.LocationPath);
    }

    [Fact]
    public async Task SignIn_WithoutOwner_RedirectsToSetup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var get = await client.GetAsync("/sign-in", ct);
        var post = await client.PostFormAsync("/sign-in", TestData.SignInFields(), ct, withToken: false);

        Assert.Equal(HttpStatusCode.Redirect, get.Status);
        Assert.Equal("/setup", get.LocationPath);
        Assert.Equal(HttpStatusCode.Redirect, post.Status);
        Assert.Equal("/setup", post.LocationPath);
        Assert.Empty(await host.AuditRowsAsync(ct));
    }

    [Fact]
    public async Task SignOut_WithoutOwner_RedirectsToSetup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.PostFormAsync("/sign-out", [], ct, withToken: false);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/setup", response.LocationPath);
    }

    [Fact]
    public async Task ErrorPage_WithoutOwner_IsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync("/error/404", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownPath_WithoutOwner_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();

        var response = await client.GetAsync("/no-such-page", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Null(response.Location);
        Assert.Contains(host.Text("Error.NotFound", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaticFile_WithoutOwner_IsServed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var client = host.CreateClient();
        var file = StaticFiles.AnyFile();

        var response = await client.GetAsync(file, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(response.Location);
    }
}
