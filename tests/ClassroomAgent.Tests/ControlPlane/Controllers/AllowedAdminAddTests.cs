using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-002, AC-006: the Owner adds an Admin, whatever the installation's status (FR-002, FR-003).</summary>
public sealed class AllowedAdminAddTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AddForm_ShowsInstallationDomainHintEmptyEmailAndToken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        var response = await owner.GetAsync(AllowedAdminTestData.AddFormPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(InstallationTestData.Name, response.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Domain, response.Text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, Html.InputValue(response.Body, "email"));
        Assert.True(Html.HasInput(response.Body, Html.AntiforgeryFieldName));
        Assert.Matches($"href=\"(https://localhost)?/installations/{installation:D}\"", response.Body);
        Assert.Matches($"<form[^>]*action=\"(https://localhost)?/installations/{installation:D}/admins\"", response.Body);
    }

    [Fact]
    public async Task Add_MixedCaseEmail_RedirectsToDetail_StoresOneLowerCaseEntry_WithOwnerAndTime()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var installation = await host.InsertInstallationAsync(ct);
        var installationId = (await host.InstallationAsync(installation, ct))!.Id;
        host.Time.Advance(TimeSpan.FromMinutes(5));

        var response = await owner.PostFromPageAsync(
            AllowedAdminTestData.AddFormPath(installation),
            AllowedAdminTestData.AddPath(installation),
            AllowedAdminTestData.AddFields("Ivan.Petrenko@SCHOOL-ONE.Example.Test"),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(AllowedAdminTestData.DetailPath(installation), response.LocationPath);
        var row = Assert.Single(await host.AllowedAdminsAsync(ct));
        Assert.Equal(AllowedAdminTestData.Email, row.Email);
        Assert.Equal(installationId, row.InstallationId);
        Assert.Equal(ownerId, row.AddedByOwnerId);
        Assert.Equal(host.Time.GetUtcNow(), row.CreatedAt);
        Assert.NotEqual(Guid.Empty, row.Identifier);
        var detail = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);
        Assert.Contains(AllowedAdminTestData.Email, detail.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("o'brien@school-one.example.test")]
    [InlineData("admin-2_x@school-one.example.test")]
    [InlineData("a@school-one.example.test")]
    public async Task Add_ValidNamePartShapes_AreAccepted(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        var row = await host.AddAllowedAdminAsync(owner, installation, email, ct);

        Assert.Equal(email, row.Email);
    }

    [Fact]
    public async Task Add_NamePartOf64Characters_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var email = new string('a', 64) + "@" + InstallationTestData.Domain;

        var row = await host.AddAllowedAdminAsync(owner, installation, email, ct);

        Assert.Equal(email, row.Email);
    }

    [Fact]
    public async Task Add_OverpostedFields_AreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var ownerId = (await host.OwnerAsync(ct))!.Id;
        var installation = await host.InsertInstallationAsync(ct);
        var forgedIdentifier = Guid.NewGuid();

        var response = await owner.PostFromPageAsync(
            AllowedAdminTestData.AddFormPath(installation),
            AllowedAdminTestData.AddPath(installation),
            [
                new("email", AllowedAdminTestData.Email),
                new("identifier", forgedIdentifier.ToString("D")),
                new("addedBy", "999"),
                new("addedByOwnerId", "999"),
                new("addedAt", "2001-01-01T00:00:00Z"),
                new("installationId", "999"),
            ],
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var row = Assert.Single(await host.AllowedAdminsAsync(ct));
        Assert.NotEqual(forgedIdentifier, row.Identifier);
        Assert.Equal(ownerId, row.AddedByOwnerId);
        Assert.Equal(host.Time.GetUtcNow(), row.CreatedAt);
    }

    [Fact]
    public async Task Add_ManyEntries_AreAllAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        for (var i = 1; i <= 12; i++)
        {
            await host.AddAllowedAdminAsync(owner, installation, $"admin{i}@{InstallationTestData.Domain}", ct);
        }

        Assert.Equal(12, (await host.AllowedAdminsAsync(installation, ct)).Count);
    }

    [Fact]
    public async Task Add_ToSuspendedInstallation_Succeeds_InstallationUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct, status: "suspended");
        var before = await host.InstallationAsync(installation, ct);

        var row = await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);

        Assert.Equal(AllowedAdminTestData.Email, row.Email);
        Assert.Equal(before, await host.InstallationAsync(installation, ct));
    }

    [Fact]
    public async Task Add_DoesNotChangeTheInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var before = await host.InstallationAsync(installation, ct);
        host.Time.Advance(TimeSpan.FromMinutes(1));

        await host.AddAllowedAdminAsync(owner, installation, AllowedAdminTestData.Email, ct);

        Assert.Equal(before, await host.InstallationAsync(installation, ct));
    }
}
