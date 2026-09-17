using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-011: the add and revoke forms require the antiforgery token; GET changes nothing (FR-010, SC-4, API-4).</summary>
public sealed class AllowedAdminAntiforgeryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Add_WithoutToken_Returns400PageExpired_CreatesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await owner.GetAsync(AllowedAdminTestData.AddFormPath(installation), ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFormAsync(
            AllowedAdminTestData.AddPath(installation),
            AllowedAdminTestData.AddFields(AllowedAdminTestData.Email),
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Revoke_WithoutToken_Returns400PageExpired_DeletesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var path = AllowedAdminTestData.RevocationPath(installation, admin);
        await owner.GetAsync(path, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFormAsync(path, [], ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Single(await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Revoke_WithTokenFromAnotherSession_Returns400_DeletesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        using var stranger = host.CreateClient();
        await stranger.GetAsync("/sign-in", ct);

        var response = await owner.SendAsync(
            HttpMethod.Post,
            AllowedAdminTestData.RevocationPath(installation, admin),
            new FormUrlEncodedContent([new(Html.AntiforgeryFieldName, stranger.LastToken!)]),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Single(await host.AllowedAdminsAsync(ct));
    }

    [Fact]
    public async Task GetRequestsWithFormValues_ChangeNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var before = await host.AllowedAdminsAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        var query = $"?email={AllowedAdminTestData.OtherEmail}&confirm=true";

        var responses = new[]
        {
            await owner.GetAsync(AllowedAdminTestData.AddFormPath(installation) + query, ct),
            await owner.GetAsync(AllowedAdminTestData.AddPath(installation) + query, ct),
            await owner.GetAsync(AllowedAdminTestData.RevocationPath(installation, admin) + query, ct),
            await owner.GetAsync(AllowedAdminTestData.RevocationPath(installation, admin), ct),
        };

        Assert.DoesNotContain(responses, r => r.Status == HttpStatusCode.Redirect);
        Assert.Equal(before, await host.AllowedAdminsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OtherMethods_DoNotAddOrRevoke(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var admin = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        await owner.GetAsync(AllowedAdminTestData.RevocationPath(installation, admin), ct);
        var before = await host.AllowedAdminsAsync(ct);

        var responses = new[]
        {
            await owner.SendAsync(new HttpMethod(method), AllowedAdminTestData.RevocationPath(installation, admin), new FormUrlEncodedContent([new(Html.AntiforgeryFieldName, owner.LastToken!)]), ct),
            await owner.SendAsync(new HttpMethod(method), AllowedAdminTestData.AddPath(installation), new FormUrlEncodedContent(AllowedAdminTestData.AddFields(AllowedAdminTestData.OtherEmail)), ct),
            await owner.SendAsync(new HttpMethod(method), $"/installations/{installation:D}/admins/{admin:D}", null, ct),
        };

        Assert.All(responses, r => Assert.True((int)r.Status >= 400, $"{method} answered {(int)r.Status}"));
        Assert.Equal(before, await host.AllowedAdminsAsync(ct));
    }
}
