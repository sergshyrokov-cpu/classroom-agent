using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>AC-011, TC-8: installation pages are translated, Ukrainian by default; entered values are never translated.</summary>
public sealed class InstallationTranslationTests(PostgreSqlFixture database)
{
    /// <summary>The keys fixed by the US-002 contract (openapi ValidationMessageKeys, PageTextKeys).</summary>
    private static readonly string[] Keys =
    [
        "Installation.Name.Required",
        "Installation.Name.Length",
        "Installation.Name.EdgeWhitespace",
        "Installation.Name.InvalidCharacters",
        "Installation.Domain.Required",
        "Installation.Domain.Length",
        "Installation.Domain.Characters",
        "Installation.Domain.NoDot",
        "Installation.Domain.Labels",
        "Installation.Domain.Idn",
        "Installation.Domain.Taken",
        "Installation.ClientId.Required",
        "Installation.ClientId.Format",
        "Installation.ClientId.Taken",
        "Installations.Empty",
        "Installation.Status.Active",
        "Installation.Status.Suspended",
        "Installation.Identifier.ConfigurationNote",
        "Installation.Identifier.Copied",
        "Installation.ClientId.ChangeNote",
        "Home.Installations",
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
    public async Task InstallationPages_AreUkrainianByDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        var emptyList = await owner.GetAsync("/installations", ct, new Dictionary<string, string> { ["Accept-Language"] = "en" });
        var identifier = await host.RegisterInstallationAsync(owner, ct);
        var clientIdPage = await owner.GetAsync($"/installations/{identifier:D}/client-id", ct);

        Assert.Equal(HttpStatusCode.OK, emptyList.Status);
        Assert.Contains("<html lang=\"uk\"", emptyList.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installations.Empty", "uk"), emptyList.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Installations.Empty", "en"), emptyList.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.ClientId.ChangeNote", "uk"), clientIdPage.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnteredValues_AreShownExactlyAsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        const string name = "Active School";
        var identifier = await host.RegisterInstallationAsync(owner, ct, name: name);

        var detail = await owner.GetAsync($"/installations/{identifier:D}", ct);

        Assert.Contains(name, detail.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.Domain, detail.Text, StringComparison.Ordinal);
        Assert.Contains(InstallationTestData.ClientId, detail.Text, StringComparison.Ordinal);
    }
}
