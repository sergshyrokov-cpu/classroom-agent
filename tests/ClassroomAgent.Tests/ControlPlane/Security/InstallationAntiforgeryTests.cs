using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-010: installation forms require the antiforgery token; nothing changes on GET (FR-011, SC-4, API-4).</summary>
public sealed class InstallationAntiforgeryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Register_WithoutToken_Returns400PageExpired_CreatesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.GetAsync("/installations/new", ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFormAsync("/installations", InstallationTestData.RegisterFields(), ct, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Empty(await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("client-id")]
    public async Task EditForm_WithoutToken_Returns400PageExpired_ChangesNothing(string page)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var path = $"/installations/{identifier:D}/{page}";
        await owner.GetAsync(path, ct);
        var before = await host.InstallationAsync(identifier, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFormAsync(
            path,
            page == "name"
                ? InstallationTestData.NameFields(InstallationTestData.OtherName)
                : InstallationTestData.ClientIdFields(InstallationTestData.OtherClientId),
            ct,
            withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(host.Text("Error.PageExpired", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.InstallationAsync(identifier, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task EditForm_WithTokenFromAnotherSession_Returns400_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        using var stranger = host.CreateClient();
        await stranger.GetAsync("/sign-in", ct);
        var before = await host.InstallationAsync(identifier, ct);

        var response = await owner.SendAsync(
            HttpMethod.Post,
            $"/installations/{identifier:D}/name",
            new FormUrlEncodedContent(
                InstallationTestData.NameFields(InstallationTestData.OtherName)
                    .Append(new(Html.AntiforgeryFieldName, stranger.LastToken!))),
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(before, await host.InstallationAsync(identifier, ct));
    }

    [Fact]
    public async Task GetRequestsWithFormValues_ChangeNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var before = await host.InstallationsAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        var query = $"?name=Changed&domain={InstallationTestData.OtherDomain}&clientId={InstallationTestData.OtherClientId}";

        await owner.GetAsync("/installations" + query, ct);
        await owner.GetAsync("/installations/new" + query, ct);
        await owner.GetAsync($"/installations/{identifier:D}" + query, ct);
        await owner.GetAsync($"/installations/{identifier:D}/name" + query, ct);
        await owner.GetAsync($"/installations/{identifier:D}/client-id" + query, ct);

        Assert.Equal(before, await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OtherMethodsOnInstallation_DoNotChangeOrDelete(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        await owner.GetAsync($"/installations/{identifier:D}", ct);
        var before = await host.InstallationsAsync(ct);

        var responses = new[]
        {
            await owner.SendAsync(new HttpMethod(method), $"/installations/{identifier:D}", new FormUrlEncodedContent(InstallationTestData.RegisterFields(domain: InstallationTestData.OtherDomain)), ct),
            await owner.SendAsync(new HttpMethod(method), $"/installations/{identifier:D}/name", new FormUrlEncodedContent(InstallationTestData.NameFields(InstallationTestData.OtherName)), ct),
        };

        Assert.All(responses, r => Assert.True((int)r.Status >= 400, $"{method} answered {(int)r.Status}"));
        Assert.Equal(before, await host.InstallationsAsync(ct));
    }
}
