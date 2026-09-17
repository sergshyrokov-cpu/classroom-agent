using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-005 AC-012: the installation detail page shows the last check — UTC time to the minute, versions,
/// answered status and compatibility — or "not called yet" (spec FR-011, I-12; api-design §9).
/// </summary>
public sealed partial class InstallationLastCheckTests(PostgreSqlFixture database)
{
    private static readonly string[] ValueElements =
    [
        "installation-last-check-time",
        "installation-last-check-application-version",
        "installation-last-check-contract-version",
        "installation-last-check-status",
        "installation-last-check-compatibility",
    ];

    [Fact]
    public async Task NeverCalled_ShowsNotCalledYet_AndNoValueElements()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Matches(ElementWithId("installation-last-check"), response.Body);
        Assert.Contains(host.Text("Installation.LastCheck.Title", "uk"), response.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.LastCheck.NotCalledYet", "uk"), ElementText(response.Body, "installation-last-check-none"), StringComparison.Ordinal);
        Assert.All(ValueElements, id => Assert.DoesNotMatch(ElementWithId(id), response.Body));
    }

    [Fact]
    public async Task AfterARealCheck_ShowsTimeVersionsStatusAndCompatibility()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        host.Time.Advance(TimeSpan.FromMinutes(7));
        var answeredAt = host.Time.GetUtcNow();
        await host.PostCheckAsync(identifier, ct, applicationVersion: "2.3.4", contractVersion: 1);
        // Within the Owner's 30-minute idle timeout (US-001), so the session is still valid.
        host.Time.Advance(TimeSpan.FromMinutes(10));

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(InstallationTestData.UkrainianUtcTime(answeredAt), ElementText(response.Body, "installation-last-check-time"));
        Assert.Equal("2.3.4", ElementText(response.Body, "installation-last-check-application-version"));
        Assert.Equal("1", ElementText(response.Body, "installation-last-check-contract-version"));
        Assert.Equal(host.Text("Installation.Status.Active", "uk"), ElementText(response.Body, "installation-last-check-status"));
        Assert.Equal(host.Text("Installation.Compatibility.Supported", "uk"), ElementText(response.Body, "installation-last-check-compatibility"));
        Assert.DoesNotMatch(ElementWithId("installation-last-check-none"), response.Body);
        foreach (var label in new[] { "Time", "ApplicationVersion", "ContractVersion", "Status", "Compatibility" })
        {
            Assert.Contains(host.Text($"Installation.LastCheck.{label}", "uk"), response.Text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("active", "supported", "Installation.Status.Active", "Installation.Compatibility.Supported")]
    [InlineData("suspended", "upgrade_recommended", "Installation.Status.Suspended", "Installation.Compatibility.UpgradeRecommended")]
    [InlineData("active", "upgrade_required", "Installation.Status.Active", "Installation.Compatibility.UpgradeRequired")]
    public async Task StoredAnswer_IsShownAsTranslatedLabels(string status, string compatibility, string statusKey, string compatibilityKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var answeredAt = new DateTimeOffset(2026, 9, 1, 23, 59, 59, TimeSpan.Zero);
        await host.InsertInstanceLicenseCheckAsync(identifier, ct, answeredAt, "10.0.7", 3, status, compatibility);

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal("01.09.2026 23:59 UTC", ElementText(response.Body, "installation-last-check-time"));
        Assert.Equal("10.0.7", ElementText(response.Body, "installation-last-check-application-version"));
        Assert.Equal("3", ElementText(response.Body, "installation-last-check-contract-version"));
        Assert.Equal(host.Text(statusKey, "uk"), ElementText(response.Body, "installation-last-check-status"));
        Assert.Equal(host.Text(compatibilityKey, "uk"), ElementText(response.Body, "installation-last-check-compatibility"));
    }

    [Fact]
    public async Task AnsweredStatus_IsTheRecordedOne_NotTheCurrentStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        await host.PostCheckAsync(identifier, ct);
        await host.SetInstallationStatusAsync(identifier, "suspended", ct);

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal(host.Text("Installation.Status.Active", "uk"), ElementText(response.Body, "installation-last-check-status"));
    }

    [Fact]
    public async Task OtherInstallationsCheck_IsNotShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var checkedOne = await host.InsertInstallationAsync(ct);
        var neverCalled = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);
        await host.PostCheckAsync(checkedOne, ct, applicationVersion: "5.6.7");

        var response = await owner.GetAsync($"/installations/{neverCalled:D}", ct);

        Assert.Matches(ElementWithId("installation-last-check-none"), response.Body);
        Assert.DoesNotContain("5.6.7", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Section_IsUkrainianByDefault_EvenWhenTheBrowserAsksForEnglish()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        await host.InsertInstanceLicenseCheckAsync(identifier, ct, host.Time.GetUtcNow(), compatibility: "upgrade_required");
        var english = new Dictionary<string, string> { ["Accept-Language"] = "en" };

        var response = await owner.GetAsync($"/installations/{identifier:D}", ct, english);

        Assert.Contains(host.Text("Installation.LastCheck.Title", "uk"), response.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Installation.LastCheck.Title", "en"), response.Text, StringComparison.Ordinal);
        Assert.Equal(host.Text("Installation.Compatibility.UpgradeRequired", "uk"), ElementText(response.Body, "installation-last-check-compatibility"));
    }

    [Fact]
    public async Task DetailPage_StaysOwnerOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        await host.PostCheckAsync(identifier, ct, applicationVersion: "8.8.8");
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in", response.LocationPath);
        Assert.DoesNotContain("8.8.8", response.Body, StringComparison.Ordinal);
    }

    private static string ElementWithId(string id) => $"<[a-z0-9]+[^>]*\\bid=\"{Regex.Escape(id)}\"[^>]*>";

    /// <summary>The decoded, trimmed text directly inside the element with that id (no nested tags expected).</summary>
    private static string ElementText(string html, string id)
    {
        var match = Regex.Match(html, ElementWithId(id) + "(?<text>[^<]*)<", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"No element with id=\"{id}\".");
        return WhiteSpace().Replace(System.Net.WebUtility.HtmlDecode(match.Groups["text"].Value), " ").Trim();
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhiteSpace();
}
