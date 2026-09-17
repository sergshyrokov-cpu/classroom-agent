using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>AC-011, TC-8: suspend and resume pages are translated, Ukrainian by default; name and domain untranslated.</summary>
public sealed class InstallationStatusTranslationTests(PostgreSqlFixture database)
{
    /// <summary>The keys fixed by the US-004 contract (openapi PageTextKeys).</summary>
    private static readonly string[] Keys =
    [
        "Installation.Suspend",
        "Installation.Resume",
        "Installation.Suspend.Explanation",
        "Installation.Suspend.Confirm",
        "Installation.Suspend.Cancel",
        "Installation.Resume.Explanation",
        "Installation.Resume.Confirm",
        "Installation.Resume.Cancel",
        "Installation.Status.AlreadySuspended",
        "Installation.Status.AlreadyActive",
    ];

    public static TheoryData<string> ContractKeys => new(Keys);

    [Theory]
    [MemberData(nameof(ContractKeys))]
    public async Task ContractKey_ExistsInUkrainianAndEnglish_AndDiffers(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(ukrainian, english);
    }

    [Fact]
    public async Task StatusPages_AreUkrainianByDefault_NameAndDomainUntranslated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var english = new Dictionary<string, string> { ["Accept-Language"] = "en" };
        var active = await host.InsertInstallationAsync(ct);
        var suspended = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId,
            status: "suspended");

        var suspendPage = await owner.GetAsync(InstallationStatusTestData.SuspensionPath(active), ct, english);
        var resumePage = await owner.GetAsync(InstallationStatusTestData.ResumptionPath(suspended), ct, english);
        var noticePage = await owner.GetAsync(
            InstallationStatusTestData.NoticePath(suspended, InstallationStatusTestData.AlreadySuspended),
            ct,
            english);

        Assert.All(new[] { suspendPage, resumePage, noticePage }, page =>
        {
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.Contains("<html lang=\"uk\"", page.Body, StringComparison.Ordinal);
        });
        Assert.Contains(host.Text("Installation.Suspend.Explanation", "uk"), suspendPage.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Installation.Suspend.Explanation", "en"), suspendPage.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.Resume.Explanation", "uk"), resumePage.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.Status.AlreadySuspended", "uk"), noticePage.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Name, suspendPage.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Domain, suspendPage.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.OtherName, resumePage.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.OtherDomain, resumePage.Text, StringComparison.Ordinal);
    }
}
