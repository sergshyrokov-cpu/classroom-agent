using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Localization;

/// <summary>
/// US-006 AC-015, TC-8: every label, warning and message the Story adds exists in Ukrainian and English,
/// and the address itself is never translated (spec FR-013; api-design §7; NFR-073).
/// </summary>
public sealed class PushAddressTranslationTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Keys => new(PushTestData.TranslationKeys);

    [Theory]
    [MemberData(nameof(Keys))]
    public async Task Key_ExistsInUkrainianAndEnglish_AndDiffers(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(ukrainian, english);
    }

    /// <summary>
    /// The UI language comes from the Owner's account, never from <c>Accept-Language</c> (US-001 FR-019;
    /// NFR-073), so the account is switched to English and the session opened again.
    /// </summary>
    [Fact]
    public async Task DetailPageInEnglish_UsesTheEnglishWarning_AndShowsTheAddressAsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var withAddress = await host.RegisterInstallationWithPushAddressAsync(owner, ct);
        var withoutAddress = await host.RegisterInstallationWithPushAddressAsync(
            owner,
            ct,
            pushAddress: null,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        await host.ExecuteAsync("UPDATE owner SET ui_language = 'en'", ct);
        var (englishOwner, _) = await host.SignInAsync(TestData.Login, TestData.Password, ct);
        using var _english = englishOwner;

        var missing = await englishOwner.GetAsync(PushTestData.DetailPath(withoutAddress), ct);
        var present = await englishOwner.GetAsync(PushTestData.DetailPath(withAddress), ct);

        Assert.Contains("<html lang=\"en\"", missing.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.PushAddress.MissingWarning", "en"), missing.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Text("Installation.PushAddress.MissingWarning", "uk"), missing.Body, StringComparison.Ordinal);
        Assert.Contains(PushTestData.Address, present.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushAddressPage_IsTranslated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var installation = await host.RegisterInstallationWithPushAddressAsync(owner, ct);

        var page = await owner.GetAsync(PushTestData.PushAddressPath(installation), ct);

        Assert.Contains(host.Text("Installation.PushAddress.Title", "uk"), page.Body, StringComparison.Ordinal);
        Assert.Contains(host.Text("Installation.PushAddress.ClearNote", "uk"), page.Body, StringComparison.Ordinal);
    }
}
