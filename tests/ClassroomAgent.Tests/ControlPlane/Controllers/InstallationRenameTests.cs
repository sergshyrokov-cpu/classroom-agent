using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-006: correcting the installation's name (FR-006, spec I-7).</summary>
public sealed class InstallationRenameTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task NameForm_IsPrefilledWithStoredName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);

        var response = await owner.GetAsync($"/installations/{identifier:D}/name", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(InstallationTestData.Name, Html.InputValue(response.Body, "name"));
        Assert.True(Html.HasInput(response.Body, Html.AntiforgeryFieldName));
        Assert.False(Html.HasInput(response.Body, "domain"));
        Assert.False(Html.HasInput(response.Body, "clientId"));
    }

    [Fact]
    public async Task ValidRename_ChangesOnlyName_RedirectsToDetail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var before = await host.InstallationAsync(identifier, ct);
        host.Time.Advance(TimeSpan.FromMinutes(5));

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/name",
            $"/installations/{identifier:D}/name",
            InstallationTestData.NameFields(InstallationTestData.OtherName),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal($"/installations/{identifier:D}", response.LocationPath);
        var after = await host.InstallationAsync(identifier, ct);
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(InstallationTestData.OtherName, after.Name);
        Assert.Equal(before with { Name = InstallationTestData.OtherName, UpdatedAt = after.UpdatedAt }, after);
        Assert.Equal(host.Time.GetUtcNow(), after.UpdatedAt);
        Assert.Contains(InstallationTestData.OtherName, (await owner.GetAsync($"/installations/{identifier:D}", ct)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rename_ToNameOfAnotherInstallation_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await host.RegisterInstallationAsync(owner, ct);
        var second = await host.RegisterInstallationAsync(
            owner,
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        var response = await owner.PostFromPageAsync(
            $"/installations/{second:D}/name",
            $"/installations/{second:D}/name",
            InstallationTestData.NameFields(InstallationTestData.Name),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.All(await host.InstallationsAsync(ct), row => Assert.Equal(InstallationTestData.Name, row.Name));
    }

    [Fact]
    public async Task UnchangedName_RedirectsToDetail_WritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var before = await host.InstallationAsync(identifier, ct);
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;
        host.Time.Advance(TimeSpan.FromMinutes(5));

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/name",
            $"/installations/{identifier:D}/name",
            InstallationTestData.NameFields(InstallationTestData.Name),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal($"/installations/{identifier:D}", response.LocationPath);
        Assert.Equal(before, await host.InstallationAsync(identifier, ct));
        Assert.Equal(auditRowsBefore, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task NameDifferingOnlyInCase_IsAChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.RegisterInstallationAsync(owner, ct, name: "ліцей");
        var auditRowsBefore = (await host.AuditRowsAsync(ct)).Count;

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/name",
            $"/installations/{identifier:D}/name",
            InstallationTestData.NameFields("Ліцей"),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("Ліцей", (await host.InstallationAsync(identifier, ct))?.Name);
        Assert.Equal(auditRowsBefore + 1, (await host.AuditRowsAsync(ct)).Count);
    }

    [Fact]
    public async Task Rename_SuspendedInstallation_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct, status: "suspended");

        var response = await owner.PostFromPageAsync(
            $"/installations/{identifier:D}/name",
            $"/installations/{identifier:D}/name",
            InstallationTestData.NameFields(InstallationTestData.OtherName),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = await host.InstallationAsync(identifier, ct);
        Assert.Equal(InstallationTestData.OtherName, row?.Name);
        Assert.Equal("suspended", row?.Status);
    }
}
