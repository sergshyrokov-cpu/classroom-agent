using System.Globalization;
using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-039 AC-007 on the installation: the two existing screens that show a date format it by the chosen language
/// (spec FR-009, NFR-073) — the read-only notice's last successful check and the Dean list's last sign-in. Times
/// stay UTC with the suffix. The Control Plane's dates already follow the culture and are covered by its existing
/// tests (InstallationLastCheckTests, AllowedAdminListTests).
/// </summary>
public sealed class DateFormatTests(PostgreSqlFixture database)
{
    private static string ShortDate(DateTimeOffset instant, string culture)
    {
        var info = CultureInfo.GetCultureInfo(culture);
        return instant.UtcDateTime.ToString(info.DateTimeFormat.ShortDatePattern, info);
    }

    /// <summary>The read-only notice: the culture's short date, <c>HH:mm</c> and <c>UTC</c>.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheLastSuccessfulCheck_FollowsTheChosenLanguage(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, _) = await DeanAccountHostExtensions.StartSignedInAsync(
            database,
            ct,
            ReadOnlyModeHost.Cause.GracePeriodExpired);
        await using var _host = host;
        using var _admin = admin;
        var lastSuccess = ReadOnlyModeHost.LastSuccessOf(ReadOnlyModeHost.Cause.GracePeriodExpired)!.Value;
        await host.ExecuteAsync("UPDATE app_user SET ui_language = @language", ct, ("language", language));
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        using var _client = client;

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(
            ShortDate(lastSuccess, language) + " " + lastSuccess.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) + " UTC",
            page.Text,
            StringComparison.Ordinal);
    }

    /// <summary>The Dean list: the culture's short date, never the fixed ISO form.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheDeansLastSignIn_FollowsTheChosenLanguage(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, admin, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _admin = admin;
        var signedInAt = new DateTimeOffset(2026, 9, 3, 7, 30, 0, TimeSpan.Zero);
        await host.InsertDeanAsync(ct, passwordIsTemporary: false, lastSuccessfulSignInAt: signedInAt);
        await host.ExecuteAsync(
            "UPDATE app_user SET ui_language = @language WHERE role = 'admin'",
            ct,
            ("language", language));
        var (client, _) = await host.SignInWithGoogleAsync(ct);
        using var _client = client;

        var page = await client.GetAsync(DeanAccountTestData.Paths.Deans, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(language, UiLanguageTestData.PageLanguage(page.Body));
        Assert.Contains(ShortDate(signedInAt, language), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-09-03", page.Text, StringComparison.Ordinal);
    }

    /// <summary>The two languages really differ for the dates used above, so the theory cannot pass on one format.</summary>
    [Fact]
    public void TheTwoLanguages_FormatTheTestDatesDifferently()
    {
        var lastSuccess = ReadOnlyModeHost.LastSuccessOf(ReadOnlyModeHost.Cause.GracePeriodExpired)!.Value;
        var signedInAt = new DateTimeOffset(2026, 9, 3, 7, 30, 0, TimeSpan.Zero);

        Assert.NotEqual(ShortDate(lastSuccess, "uk"), ShortDate(lastSuccess, "en"));
        Assert.NotEqual(ShortDate(signedInAt, "uk"), ShortDate(signedInAt, "en"));
    }
}
