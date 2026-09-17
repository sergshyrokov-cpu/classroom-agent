using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-010: suspend and resume require the antiforgery token; GET changes nothing (FR-009, SC-4, API-4).</summary>
public sealed class InstallationStatusAntiforgeryTests(PostgreSqlFixture database)
{
    [Theory]
    [InlineData("active", "suspension")]
    [InlineData("suspended", "resumption")]
    public async Task Post_WithoutToken_Returns400PageExpired_StatusUnchanged(string status, string resource)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: status);
        var path = $"/installations/{installation:D}/{resource}";
        await owner.GetAsync(path, ct);
        var before = await host.InstallationAsync(installation, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFormAsync(path, [], ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.InstallationAsync(installation, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Suspend_WithTokenFromAnotherSession_Returns400_StatusUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        using var stranger = host.CreateClient();
        await stranger.GetAsync("/sign-in", ct);

        var response = await owner.SendAsync(
            HttpMethod.Post,
            InstallationStatusTestData.SuspensionPath(installation),
            new FormUrlEncodedContent([new(Html.AntiforgeryFieldName, stranger.LastToken!)]),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("active", (await host.InstallationAsync(installation, ct))!.Status);
    }

    [Fact]
    public async Task GetRequests_WithQueryValues_ChangeNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var active = await host.InsertInstallationAsync(ct);
        var suspended = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId,
            status: "suspended");
        var before = await host.InstallationsAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        const string query = "?confirm=true&status=suspended";

        var responses = new[]
        {
            await owner.GetAsync(InstallationStatusTestData.SuspensionPath(active) + query, ct),
            await owner.GetAsync(InstallationStatusTestData.ResumptionPath(suspended) + query, ct),
            await owner.GetAsync(InstallationStatusTestData.DetailPath(active) + query, ct),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.Equal(before, await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OtherMethods_DoNotChangeStatus(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await owner.LoadTokenAsync(ct);
        var before = await host.InstallationsAsync(ct);

        var responses = new[]
        {
            await owner.SendAsync(new HttpMethod(method), InstallationStatusTestData.SuspensionPath(installation), new FormUrlEncodedContent([new(Html.AntiforgeryFieldName, owner.LastToken!)]), ct),
            await owner.SendAsync(new HttpMethod(method), InstallationStatusTestData.ResumptionPath(installation), new FormUrlEncodedContent([new(Html.AntiforgeryFieldName, owner.LastToken!)]), ct),
            await owner.SendAsync(new HttpMethod(method), $"/installations/{installation:D}/status", new FormUrlEncodedContent([new("status", "suspended")]), ct),
        };

        Assert.All(responses, r => Assert.True((int)r.Status >= 400, $"{method} answered {(int)r.Status}"));
        Assert.Equal(before, await host.InstallationsAsync(ct));
    }
}
