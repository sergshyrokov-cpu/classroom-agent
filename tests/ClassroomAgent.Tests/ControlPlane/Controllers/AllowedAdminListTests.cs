using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>AC-001, AC-007: the Admins section of the detail page and the fewer-than-two warning (FR-001, FR-006).</summary>
public sealed class AllowedAdminListTests(PostgreSqlFixture database)
{
    private const string WarningElement = "id=\"allowed-admins-warning\"";

    [Fact]
    public async Task Detail_NoEntries_ShowsEmptyMessageWarningAndAddLink()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);

        var response = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(host.Text("AllowedAdmins.Title", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("AllowedAdmins.Empty", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(WarningElement, response.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text("AllowedAdmins.FewerThanTwoWarning", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Matches($"href=\"(https://localhost)?/installations/{installation:D}/admins/new\"", response.Body);
    }

    [Fact]
    public async Task Detail_ListsOnlyThisInstallationsEntries_OrderedByEmail_WithDateAdded()
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
        var addedEarlier = new DateTimeOffset(2026, 9, 17, 9, 33, 59, TimeSpan.Zero);
        var addedLater = new DateTimeOffset(2026, 9, 18, 14, 5, 0, TimeSpan.Zero);
        await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.OtherEmail, ct, addedEarlier);
        await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct, addedLater);
        await host.InsertAllowedAdminAsync(other, AllowedAdminTestData.OtherSchoolEmail, ct);

        var response = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.DoesNotContain(AllowedAdminTestData.OtherSchoolEmail, response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("AllowedAdmins.Empty", "uk"), response.Text, StringComparison.Ordinal);
        var ivan = response.Text.IndexOf(AllowedAdminTestData.Email, StringComparison.Ordinal);
        var olena = response.Text.IndexOf(AllowedAdminTestData.OtherEmail, StringComparison.Ordinal);
        Assert.True(ivan >= 0 && olena >= 0, "Both entries of the installation are listed.");
        Assert.True(ivan < olena, "Entries are ordered by email.");
        Assert.Contains(InstallationTestData.UkrainianUtcTime(addedEarlier), response.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.UkrainianUtcTime(addedLater), response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_EveryEntryHasRevokeLink_AndAddLinkStays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var first = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);
        var second = await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.OtherEmail, ct);

        var response = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Matches($"href=\"(https://localhost)?{Regex.Escape(AllowedAdminTestData.RevocationPath(installation, first))}\"", response.Body);
        Assert.Matches($"href=\"(https://localhost)?{Regex.Escape(AllowedAdminTestData.RevocationPath(installation, second))}\"", response.Body);
        Assert.Matches($"href=\"(https://localhost)?/installations/{installation:D}/admins/new\"", response.Body);
        Assert.Contains(host.Text("AllowedAdmin.Revoke", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("AllowedAdmins.Add", "uk"), response.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public async Task Detail_WarningShownOnlyWithFewerThanTwoEntries(int entries, bool warned)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        var emails = new[] { AllowedAdminTestData.Email, AllowedAdminTestData.OtherEmail, AllowedAdminTestData.ThirdEmail };
        foreach (var email in emails.Take(entries))
        {
            await host.InsertAllowedAdminAsync(installation, email, ct);
        }

        var response = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(warned, response.Body.Contains(WarningElement, StringComparison.Ordinal));
        Assert.Equal(warned, response.Text.Contains(host.Text("AllowedAdmins.FewerThanTwoWarning", "uk"), StringComparison.Ordinal));
        Assert.Matches($"href=\"(https://localhost)?/installations/{installation:D}/admins/new\"", response.Body);
    }

    [Fact]
    public async Task Detail_US002ContentStays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        await host.InsertAllowedAdminAsync(installation, AllowedAdminTestData.Email, ct);

        var detail = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);
        var list = await owner.GetAsync("/installations", ct);

        Assert.Contains(installation.ToString("D"), detail.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.ClientId, detail.Text, StringComparison.Ordinal);
        Assert.Matches($"href=\"(https://localhost)?/installations/{installation:D}/name\"", detail.Body);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.DoesNotContain(AllowedAdminTestData.Email, list.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(WarningElement, list.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_EmailWithApostrophe_IsHtmlEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.InsertInstallationAsync(ct);
        const string email = "o'brien@school-one.example.test";
        await host.InsertAllowedAdminAsync(installation, email, ct);

        var response = await owner.GetAsync(AllowedAdminTestData.DetailPath(installation), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(email, response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(email, response.Body, StringComparison.Ordinal);
    }
}
