using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-028 AC-004, FR-013, NFR-073: every program text of the file and every new message exists in Ukrainian and English.
/// The file's texts are checked through the text port itself (entity model §3.2), so the test does not depend on which
/// key a member maps to; the message keys are those the API design names.
/// </summary>
public sealed class JournalExportTranslationTests(PostgreSqlFixture database)
{
    public static TheoryData<ReportText> AllTexts => new(Enum.GetValues<ReportText>());

    /// <summary>FR-007, FR-013: every member of the text port resolves in both languages — a missing key would answer the same key in both.</summary>
    [Theory]
    [MemberData(nameof(AllTexts))]
    public async Task EveryFileText_ResolvesInBothLanguages(ReportText text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.ReportText(text, "uk");
        var english = host.ReportText(text, "en");

        Assert.False(string.IsNullOrWhiteSpace(ukrainian));
        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.NotEqual(ukrainian, english);
    }

    /// <summary>FR-004.9: both sheet names fit Excel's 31-character limit in both languages.</summary>
    [Theory]
    [InlineData(ReportText.SheetGrading)]
    [InlineData(ReportText.SheetLessonTopics)]
    public async Task SheetNames_FitExcelsLimit(ReportText text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.InRange(host.ReportText(text, "uk").Length, 1, 31);
        Assert.InRange(host.ReportText(text, "en").Length, 1, 31);
    }

    /// <summary>A program mark resolves through the same keys as the report page.</summary>
    [Fact]
    public async Task AProgramMark_ResolvesInBothLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.Equal(host.Text(ClassroomAgent.Web.Security.ReportTemplateTextKeys.ProgramMark("Late"), "uk"), ProgramMark(host, "Late", "uk"));
        Assert.NotEqual(ProgramMark(host, "Late", "uk"), ProgramMark(host, "Late", "en"));
    }

    /// <summary>FR-013, api-design §5: the new messages exist in both languages and differ.</summary>
    [Theory]
    [InlineData("Api.Error.SignInRequired")]
    [InlineData("Api.Error.PasswordChangeRequired")]
    [InlineData("Export.Validation.RequestMalformed")]
    [InlineData("Export.Validation.OrientationInvalid")]
    [InlineData("Report.Export.Action")]
    public async Task EveryNewMessage_ExistsInBothLanguages(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.NotEqual(key, ukrainian);
        Assert.NotEqual(key, english);
        Assert.NotEqual(ukrainian, english);
    }

    private static string ProgramMark(InstallationTestHost host, string programKey, string culture)
    {
        using var scope = host.CreateScope();
        var localizer = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<ClassroomAgent.Application.Localization.SharedResource>>(scope.ServiceProvider);
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
        try
        {
            return new ClassroomAgent.Web.Security.LocalizedReportTexts(localizer).ProgramMark(programKey);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }
}
