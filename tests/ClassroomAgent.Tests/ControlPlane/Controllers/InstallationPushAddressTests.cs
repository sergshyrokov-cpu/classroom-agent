using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-006 AC-002: the Owner sets the push address at registration or on its own page, changes and clears
/// it at any status; the value is stored canonically, is not unique and an unchanged submission writes
/// nothing (spec FR-002, VR-001; api-design §7).
/// </summary>
public sealed class InstallationPushAddressTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Registration_WithoutAPushAddress_StoresNone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        Assert.Null(await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task Registration_WithAPushAddress_StoresIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        Assert.Equal(PushTestData.Address, await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task RegistrationForm_OffersThePushAddressField()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var form = await owner.GetAsync("/installations/new", ct);

        Assert.Equal(HttpStatusCode.OK, form.Status);
        Assert.Contains(PushTestData.FieldName, form.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushAddressForm_ShowsTheStoredValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var form = await owner.GetAsync(PushTestData.PushAddressPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, form.Status);
        Assert.Equal(PushTestData.Address, Html.InputValue(form.Body, PushTestData.FieldName));
    }

    [Fact]
    public async Task PushAddressForm_WithoutAStoredValue_IsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var form = await owner.GetAsync(PushTestData.PushAddressPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, form.Status);
        Assert.True(string.IsNullOrEmpty(Html.InputValue(form.Body, PushTestData.FieldName)));
    }

    [Fact]
    public async Task PushAddressForm_ForAnUnknownInstallation_IsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var form = await owner.GetAsync(
            PushTestData.PushAddressPath(Guid.Parse(InstallationTestData.UnknownIdentifier)),
            ct);

        Assert.Equal(HttpStatusCode.NotFound, form.Status);
    }

    [Fact]
    public async Task SettingTheAddress_StoresItAndRedirectsToTheDetailPage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var response = await owner.ChangePushAddressAsync(installation, PushTestData.Address, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(PushTestData.DetailPath(installation), response.LocationPath);
        Assert.Equal(PushTestData.Address, await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task ChangingTheAddress_ReplacesIt_AndLeavesEveryOtherColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        var before = await host.InstallationAsync(installation, ct);

        await owner.ChangePushAddressAsync(installation, PushTestData.OtherAddress, ct);

        var after = await host.InstallationAsync(installation, ct);
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(PushTestData.OtherAddress, await host.PushAddressAsync(installation, ct));
        Assert.Equal(before.Identifier, after.Identifier);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Domain, after.Domain);
        Assert.Equal(before.ClientId, after.ClientId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
    }

    [Fact]
    public async Task ClearingTheAddress_StoresNone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var response = await owner.ChangePushAddressAsync(installation, string.Empty, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Null(await host.PushAddressAsync(installation, ct));
    }

    [Theory]
    [InlineData("  http://10.0.0.5:8081  ", "http://10.0.0.5:8081")]
    [InlineData("http://10.0.0.5:8081/", "http://10.0.0.5:8081")]
    [InlineData("HTTP://School-A.Private:8081", "http://school-a.private:8081")]
    [InlineData("http://[FD00::5]:8081", "http://[fd00::5]:8081")]
    public async Task AddressIsStoredCanonically(string entered, string stored)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        await owner.ChangePushAddressAsync(installation, entered, ct);

        Assert.Equal(stored, await host.PushAddressAsync(installation, ct));
    }

    [Fact]
    public async Task SubmittingTheSameCanonicalValue_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        var before = await host.InstallationAsync(installation, ct);

        var response = await owner.ChangePushAddressAsync(installation, PushTestData.Address + "/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(PushTestData.DetailPath(installation), response.LocationPath);
        Assert.Equal(before!.UpdatedAt, (await host.InstallationAsync(installation, ct))!.UpdatedAt);
    }

    [Fact]
    public async Task SubmittingEmptyWhileNoneIsStored_ChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        var before = await host.InstallationAsync(installation, ct);

        var response = await owner.ChangePushAddressAsync(installation, "   ", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Null(await host.PushAddressAsync(installation, ct));
        Assert.Equal(before!.UpdatedAt, (await host.InstallationAsync(installation, ct))!.UpdatedAt);
    }

    [Fact]
    public async Task TwoInstallations_MayShareOneAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var first = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var second = await host.RegisterInstallationWithPushAddressAsync(
            owner,
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        Assert.Equal(PushTestData.Address, await host.PushAddressAsync(first, ct));
        Assert.Equal(PushTestData.Address, await host.PushAddressAsync(second, ct));
    }

    [Fact]
    public async Task SuspendedInstallation_AcceptsAnAddressChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        await host.SetInstallationStatusAsync(installation, "suspended", ct);

        var response = await owner.ChangePushAddressAsync(installation, PushTestData.Address, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(PushTestData.Address, await host.PushAddressAsync(installation, ct));
        Assert.Equal("suspended", (await host.InstallationAsync(installation, ct))!.Status);
    }

    [Fact]
    public async Task ChangingTheAddressOfAnUnknownInstallation_IsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.LoadTokenAsync(ct);

        var response = await owner.PostFormAsync(
            PushTestData.PushAddressPath(Guid.Parse(InstallationTestData.UnknownIdentifier)),
            PushTestData.AddressFields(PushTestData.Address),
            ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    [Fact]
    public async Task UnknownInstallation_IsNotFound_EvenWithAnInvalidAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        await owner.LoadTokenAsync(ct);

        var response = await owner.PostFormAsync(
            PushTestData.PushAddressPath(Guid.Parse(InstallationTestData.UnknownIdentifier)),
            PushTestData.AddressFields("not-an-address"),
            ct);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }
}
