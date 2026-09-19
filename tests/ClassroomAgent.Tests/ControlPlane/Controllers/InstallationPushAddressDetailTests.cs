using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-006 AC-004: the school's page shows the push address, or "not set" with the warning that a status
/// change then reaches the school only with the periodic check; both states link to the push address page
/// and no push result is ever shown (spec FR-004; api-design §7).
/// </summary>
public sealed class InstallationPushAddressDetailTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task WithAnAddress_ThePageShowsIt_WithoutTheWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var page = await owner.GetAsync(PushTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(PushTestData.Address, Html.ElementText(page.Body, "installation-push-address"));
        Assert.Null(Html.ElementText(page.Body, "installation-push-address-none"));
        Assert.Null(Html.ElementText(page.Body, "installation-push-address-warning"));
    }

    [Fact]
    public async Task WithoutAnAddress_ThePageSaysSo_AndWarns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);

        var page = await owner.GetAsync(PushTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Null(Html.ElementText(page.Body, "installation-push-address"));
        Assert.Equal(host.Text("Installation.PushAddress.NotSet", "uk"), Html.ElementText(page.Body, "installation-push-address-none"));
        Assert.Equal(
            host.Text("Installation.PushAddress.MissingWarning", "uk"),
            Html.ElementText(page.Body, "installation-push-address-warning"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothStates_LinkToThePushAddressPage(bool withAddress)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(
            owner,
            ct,
            pushAddress: withAddress ? PushTestData.Address : null);

        var page = await owner.GetAsync(PushTestData.DetailPath(installation), ct);

        Assert.NotNull(Html.ElementText(page.Body, "installation-push-address-change"));
        Assert.Contains(PushTestData.PushAddressPath(installation), page.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePageShowsNothingAboutPushResults()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var page = await owner.GetAsync(PushTestData.DetailPath(installation), ct);

        Assert.Null(Html.ElementText(page.Body, "installation-push-delivery"));
        Assert.Null(Html.ElementText(page.Body, "installation-push-result"));
    }

    [Fact]
    public async Task TheStoredAddressIsShownExactlyAsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct, pushAddress: null);
        await owner.ChangePushAddressAsync(installation, "HTTP://School-A.Private:8081/", ct);

        var page = await owner.GetAsync(PushTestData.DetailPath(installation), ct);

        Assert.Equal("http://school-a.private:8081", Html.ElementText(page.Body, "installation-push-address"));
        Assert.Equal(
            await host.PushAddressAsync(installation, ct),
            Html.ElementText(page.Body, "installation-push-address"));
    }

    [Fact]
    public async Task ThePageKeepsEverythingEarlierStoriesShow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var page = await owner.GetAsync(PushTestData.DetailPath(installation), ct);

        Assert.Equal(installation.ToString("D"), Html.ElementText(page.Body, "installation-identifier"));
        Assert.Contains(InstallationTestData.Domain, page.Body, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.ClientId, page.Body, StringComparison.Ordinal);
        Assert.NotNull(Html.ElementText(page.Body, "installation-last-check-none"));
    }
}
