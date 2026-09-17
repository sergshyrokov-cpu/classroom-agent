using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-004: a domain and a client ID belong to one installation, also under concurrency (FR-008).</summary>
public sealed class InstallationUniquenessTests(PostgreSqlFixture database)
{
    [Theory]
    [InlineData("school-one.example.test")]
    [InlineData("SCHOOL-ONE.Example.Test")]
    public async Task Register_DomainAlreadyRegistered_AnyCase_Returns409OnDomain(string domain)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.RegisterInstallationAsync(owner, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(InstallationTestData.OtherName, domain, InstallationTestData.OtherClientId),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Null(response.Location);
        Assert.Contains(host.Text("Installation.Domain.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Installation.ClientId.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(domain, Html.InputValue(response.Body, "domain"));
        Assert.Equal(InstallationTestData.OtherName, Html.InputValue(response.Body, "name"));
        Assert.DoesNotContain(InstallationTestData.Name, response.Text, StringComparison.Ordinal);
        Assert.Single(await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Register_DomainOfSuspendedInstallation_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.InsertInstallationAsync(ct, status: "suspended");

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.Domain, InstallationTestData.OtherClientId),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text("Installation.Domain.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Single(await host.InstallationsAsync(ct));
    }

    [Fact]
    public async Task Register_ClientIdAlreadyRegistered_Returns409OnClientId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.RegisterInstallationAsync(owner, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.OtherDomain, InstallationTestData.ClientId),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text("Installation.ClientId.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Installation.Domain.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(InstallationTestData.ClientId, Html.InputValue(response.Body, "clientId"));
        Assert.Single(await host.InstallationsAsync(ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Register_DomainAndClientIdBothRegistered_ReportsBothFields()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.RegisterInstallationAsync(owner, ct);

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.Domain, InstallationTestData.ClientId),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text("Installation.Domain.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.ClientId.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Single(await host.InstallationsAsync(ct));
    }

    [Fact]
    public async Task Register_DomainAndClientIdHeldByDifferentInstallations_ReportsBothFields()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.RegisterInstallationAsync(owner, ct);
        await host.RegisterInstallationAsync(
            owner,
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        var response = await owner.PostFromPageAsync(
            "/installations/new",
            "/installations",
            InstallationTestData.RegisterFields("Третя школа", InstallationTestData.Domain, InstallationTestData.OtherClientId),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text("Installation.Domain.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.ClientId.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(2, (await host.InstallationsAsync(ct)).Count);
    }

    [Fact]
    public async Task ChangeClientId_ToAnotherInstallationsClientId_Returns409_Unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var first = await host.RegisterInstallationAsync(owner, ct);
        await host.RegisterInstallationAsync(
            owner,
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFromPageAsync(
            $"/installations/{first:D}/client-id",
            $"/installations/{first:D}/client-id",
            InstallationTestData.ClientIdFields(InstallationTestData.OtherClientId),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains(host.Text("Installation.ClientId.Taken", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Equal(InstallationTestData.OtherClientId, Html.InputValue(response.Body, "clientId"));
        Assert.DoesNotContain(InstallationTestData.OtherName, response.Text, StringComparison.Ordinal);
        Assert.Equal(InstallationTestData.ClientId, (await host.InstallationAsync(first, ct))?.ClientId);
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task ConcurrentRegistrations_SameDomainDifferentCase_OneCreated_Other409()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var first = await host.CreateOwnerAsync(ct);
        var (second, _) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using (second)
        {
            await first.GetAsync("/installations/new", ct);
            await second.GetAsync("/installations/new", ct);
            var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

            var responses = await Task.WhenAll(
                first.PostFormAsync(
                    "/installations",
                    InstallationTestData.RegisterFields(InstallationTestData.Name, InstallationTestData.Domain, InstallationTestData.ClientId),
                    ct),
                second.PostFormAsync(
                    "/installations",
                    InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.Domain.ToUpperInvariant(), InstallationTestData.OtherClientId),
                    ct));

            Assert.Equal(
                [HttpStatusCode.Redirect, HttpStatusCode.Conflict],
                responses.Select(r => r.Status).Order().ToArray());
            Assert.Contains(host.Text("Installation.Domain.Taken", "uk"), responses.Single(r => r.Status == HttpStatusCode.Conflict).Text, StringComparison.Ordinal);
            Assert.Single(await host.InstallationsAsync(ct));
            var newRows = (await host.AuditRowsAsync(ct)).Skip(auditRowsBefore).ToList();
            var row = Assert.Single(newRows);
            Assert.Equal("installation_created", row.Action);
        }
    }

    [Fact]
    public async Task ConcurrentRegistrations_SameClientId_OneCreated_Other409()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var first = await host.CreateOwnerAsync(ct);
        var (second, _) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using (second)
        {
            await first.GetAsync("/installations/new", ct);
            await second.GetAsync("/installations/new", ct);
            var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

            var responses = await Task.WhenAll(
                first.PostFormAsync(
                    "/installations",
                    InstallationTestData.RegisterFields(InstallationTestData.Name, InstallationTestData.Domain, InstallationTestData.ClientId),
                    ct),
                second.PostFormAsync(
                    "/installations",
                    InstallationTestData.RegisterFields(InstallationTestData.OtherName, InstallationTestData.OtherDomain, InstallationTestData.ClientId),
                    ct));

            Assert.Equal(
                [HttpStatusCode.Redirect, HttpStatusCode.Conflict],
                responses.Select(r => r.Status).Order().ToArray());
            Assert.Contains(host.Text("Installation.ClientId.Taken", "uk"), responses.Single(r => r.Status == HttpStatusCode.Conflict).Text, StringComparison.Ordinal);
            Assert.Single(await host.InstallationsAsync(ct));
            Assert.Single((await host.AuditRowsAsync(ct)).Skip(auditRowsBefore));
        }
    }
}
