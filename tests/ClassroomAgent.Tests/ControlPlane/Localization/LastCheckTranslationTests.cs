using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>US-005 AC-012, TC-8: every key of the last-check section exists in Ukrainian and English (api-design §9).</summary>
public sealed class LastCheckTranslationTests(PostgreSqlFixture database)
{
    private static readonly string[] Keys =
    [
        "Installation.LastCheck.Title",
        "Installation.LastCheck.Time",
        "Installation.LastCheck.ApplicationVersion",
        "Installation.LastCheck.ContractVersion",
        "Installation.LastCheck.Status",
        "Installation.LastCheck.Compatibility",
        "Installation.LastCheck.NotCalledYet",
        "Installation.Compatibility.Supported",
        "Installation.Compatibility.UpgradeRecommended",
        "Installation.Compatibility.UpgradeRequired",
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
}
