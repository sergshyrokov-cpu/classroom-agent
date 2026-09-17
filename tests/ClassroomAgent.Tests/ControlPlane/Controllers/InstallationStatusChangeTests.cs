using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-001 … AC-005, AC-007: status action, confirmations, suspending and resuming, unknown targets (FR-001 … FR-003, FR-007).</summary>
public sealed class InstallationStatusChangeTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Detail_ActiveInstallation_OffersSuspendOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        var detail = await owner.GetAsync(InstallationStatusTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.Contains(InstallationStatusTestData.SuspendElement, detail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationStatusTestData.ResumeElement, detail.Body, StringComparison.Ordinal);
        Assert.Matches(Href(InstallationStatusTestData.SuspensionPath(installation)), detail.Body);
        Assert.DoesNotMatch(Href(InstallationStatusTestData.ResumptionPath(installation)), detail.Body);
        Assert.Contains(host.Text("Installation.Suspend", "uk"), detail.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.Status.Active", "uk"), detail.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationStatusTestData.NoticeElement, detail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_SuspendedInstallation_OffersResumeOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: "suspended");

        var detail = await owner.GetAsync(InstallationStatusTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.Contains(InstallationStatusTestData.ResumeElement, detail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationStatusTestData.SuspendElement, detail.Body, StringComparison.Ordinal);
        Assert.Matches(Href(InstallationStatusTestData.ResumptionPath(installation)), detail.Body);
        Assert.DoesNotMatch(Href(InstallationStatusTestData.SuspensionPath(installation)), detail.Body);
        Assert.Contains(host.Text("Installation.Resume", "uk"), detail.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.Status.Suspended", "uk"), detail.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuspendConfirmation_ShowsNameDomainExplanationFormAndCancel_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var before = await host.InstallationAsync(installation, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        var path = InstallationStatusTestData.SuspensionPath(installation);

        var response = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        AssertConfirmationPage(host, response, installation, path, "Installation.Suspend");
        Assert.Equal(before, await host.InstallationAsync(installation, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task ResumeConfirmation_ShowsNameDomainExplanationFormAndCancel_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: "suspended");
        var before = await host.InstallationAsync(installation, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        var path = InstallationStatusTestData.ResumptionPath(installation);

        var response = await owner.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        AssertConfirmationPage(host, response, installation, path, "Installation.Resume");
        Assert.Equal(before, await host.InstallationAsync(installation, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Cancel_IsALinkToTheDetailPage_StatusUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        var confirmation = await owner.GetAsync(InstallationStatusTestData.SuspensionPath(installation), ct);
        var detail = await owner.GetAsync(InstallationStatusTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, confirmation.Status);
        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.Equal("active", (await host.InstallationAsync(installation, ct))!.Status);
        Assert.Contains(InstallationStatusTestData.SuspendElement, detail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Suspend_ChangesOnlyStatus_RedirectsToDetail_WhichOffersResume()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.OtherEmail, ct);
        var before = (await host.InstallationAsync(installation, ct))!;
        var adminsBefore = await host.AllowedAdminsAsync(ct);
        await owner.GetAsync(InstallationStatusTestData.SuspensionPath(installation), ct);
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.PostFormAsync(InstallationStatusTestData.SuspensionPath(installation), [], ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(InstallationStatusTestData.DetailPath(installation), response.LocationPath);
        var after = (await host.InstallationAsync(installation, ct))!;
        Assert.Equal(before with { Status = "suspended", UpdatedAt = host.Time.GetUtcNow() }, after);
        Assert.Equal(adminsBefore, await host.AllowedAdminsAsync(ct));
        var detail = await owner.GetAsync(InstallationStatusTestData.DetailPath(installation), ct);
        Assert.Contains(InstallationStatusTestData.ResumeElement, detail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationStatusTestData.NoticeElement, detail.Body, StringComparison.Ordinal);
        var list = await owner.GetAsync("/installations", ct);
        Assert.Contains(host.Text("Installation.Status.Suspended", "uk"), list.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resume_ChangesOnlyStatus_RedirectsToDetail_WhichOffersSuspend()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: "suspended");
        await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var before = (await host.InstallationAsync(installation, ct))!;
        var adminsBefore = await host.AllowedAdminsAsync(ct);
        await owner.GetAsync(InstallationStatusTestData.ResumptionPath(installation), ct);
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.PostFormAsync(InstallationStatusTestData.ResumptionPath(installation), [], ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(InstallationStatusTestData.DetailPath(installation), response.LocationPath);
        var after = (await host.InstallationAsync(installation, ct))!;
        Assert.Equal(before with { Status = "active", UpdatedAt = host.Time.GetUtcNow() }, after);
        Assert.Equal(adminsBefore, await host.AllowedAdminsAsync(ct));
        var detail = await owner.GetAsync(InstallationStatusTestData.DetailPath(installation), ct);
        Assert.Contains(InstallationStatusTestData.SuspendElement, detail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuspendAndResume_CanRepeat_EachChangeAudited()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await owner.SuspendInstallationAsync(installation, ct)).Status);
            Assert.Equal("suspended", (await host.InstallationAsync(installation, ct))!.Status);
            statuses.Add((await owner.ResumeInstallationAsync(installation, ct)).Status);
            Assert.Equal("active", (await host.InstallationAsync(installation, ct))!.Status);
        }

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.Redirect, s));
        Assert.Equal(
            new[] { "installation_suspended", "installation_resumed", "installation_suspended", "installation_resumed", "installation_suspended", "installation_resumed" },
            (await host.AuditRowsAsync(ct)).Skip(auditRowsBefore).Select(r => r.Action));
    }

    [Fact]
    public async Task PostedFields_AreIgnored_OnlyStatusChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var before = (await host.InstallationAsync(installation, ct))!;
        await owner.LoadTokenAsync(ct);
        host.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await owner.PostFormAsync(
            InstallationStatusTestData.SuspensionPath(installation),
            [
                new("status", "active"),
                new("name", "Hijacked"),
                new("domain", "evil.example.test"),
                new("clientId", "999999999999"),
                new("identifier", Guid.NewGuid().ToString("D")),
                new("reason", "note"),
            ],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(
            before with { Status = "suspended", UpdatedAt = host.Time.GetUtcNow() },
            await host.InstallationAsync(installation, ct));
    }

    [Fact]
    public async Task OtherInstallation_IsNotAffected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var other = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);
        var otherBefore = await host.InstallationAsync(other, ct);

        var response = await owner.SuspendInstallationAsync(installation, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(otherBefore, await host.InstallationAsync(other, ct));
    }

    [Fact]
    public async Task UnknownInstallation_AllFourOperations_Return404_ChangeNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var unknown = Guid.Parse(InstallationTestData.UnknownIdentifier);
        var before = await host.InstallationsAsync(ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var responses = new[]
        {
            await owner.GetAsync(InstallationStatusTestData.SuspensionPath(unknown), ct),
            await owner.GetAsync(InstallationStatusTestData.ResumptionPath(unknown), ct),
            await owner.SuspendInstallationAsync(unknown, ct),
            await owner.ResumeInstallationAsync(unknown, ct),
        };

        Assert.All(responses, r =>
        {
            Assert.Equal(HttpStatusCode.NotFound, r.Status);
            Assert.Contains(host.Text("Error.NotFound", "uk"), r.Text, StringComparison.Ordinal);
        });
        Assert.Equal(before, await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
        Assert.NotEqual(unknown, installation);
    }

    [Theory]
    [InlineData("GET", "/installations/not-a-uuid/suspension")]
    [InlineData("GET", "/installations/not-a-uuid/resumption")]
    [InlineData("GET", "/installations/12345/suspension")]
    [InlineData("POST", "/installations/not-a-uuid/suspension")]
    [InlineData("POST", "/installations/not-a-uuid/resumption")]
    public async Task NonUuidIdentifier_Returns404ErrorPage(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.InsertInstallationAsync(ct);
        var before = await host.InstallationsAsync(ct);
        await owner.LoadTokenAsync(ct);

        var response = method == "GET"
            ? await owner.GetAsync(path, ct)
            : await owner.PostFormAsync(path, [], ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.InstallationsAsync(ct));
    }

    private static void AssertConfirmationPage(
        ControlPlaneTestHost host,
        PageResponse response,
        Guid installation,
        string path,
        string keyPrefix)
    {
        Assert.Contains(InstallationTestData.Name, response.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Domain, response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(keyPrefix + ".Explanation", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(keyPrefix + ".Confirm", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(keyPrefix + ".Cancel", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Matches(
            $"<form[^>]*method=\"post\"[^>]*action=\"(https://localhost)?{Regex.Escape(path)}\"|<form[^>]*action=\"(https://localhost)?{Regex.Escape(path)}\"[^>]*method=\"post\"",
            response.Body);
        Assert.True(Html.HasInput(response.Body, Html.AntiforgeryFieldName));
        Assert.Matches(Href(InstallationStatusTestData.DetailPath(installation)), response.Body);
        Assert.DoesNotMatch("<script(?![^>]*\\bsrc=)[^>]*>", response.Body);
        Assert.DoesNotMatch("\\son[a-z]+\\s*=", response.Body);
    }

    private static string Href(string path) => $"<a[^>]*href=\"(https://localhost)?{Regex.Escape(path)}\"";
}
