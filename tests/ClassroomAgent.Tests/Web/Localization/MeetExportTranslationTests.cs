using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Localization;

/// <summary>
/// US-044 AC-014, FR-015, NFR-073: every program text of the Meet workbooks and the new message exist in Ukrainian and
/// English; an unresolved key would echo its own name. The texts are read through the text port itself.
/// </summary>
public sealed class MeetExportTranslationTests(PostgreSqlFixture database)
{
    /// <summary>The two columns that may legitimately read "Email" in both languages.</summary>
    private static readonly ReportText[] SameInBothLanguages = [ReportText.MeetColumnEmail, ReportText.MeetColumnOrganizerEmail];

    private static IEnumerable<ReportText> MeetTexts() =>
        Enum.GetValues<ReportText>().Where(t => t.ToString().StartsWith("Meet", StringComparison.Ordinal));

    public static TheoryData<ReportText> AllMeetTexts => new(MeetTexts());

    public static TheoryData<ReportText> DifferingMeetTexts => new(MeetTexts().Except(SameInBothLanguages));

    /// <summary>FR-015: every Meet member resolves in both languages — a missing key would echo the key.</summary>
    [Theory]
    [MemberData(nameof(AllMeetTexts))]
    public async Task EveryMeetText_ResolvesInBothLanguages(ReportText text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        foreach (var language in new[] { "uk", "en" })
        {
            var value = host.ReportText(text, language);

            Assert.False(string.IsNullOrWhiteSpace(value), $"{text} is empty in {language}");
            Assert.NotEqual(text.ToString(), value);
            Assert.DoesNotContain("MeetReports.", value, StringComparison.Ordinal);
            Assert.DoesNotContain("Export.", value, StringComparison.Ordinal);
        }
    }

    /// <summary>FR-015: sheet names, header labels and Summary labels differ between the languages.</summary>
    [Theory]
    [MemberData(nameof(DifferingMeetTexts))]
    public async Task EveryMeetText_DiffersBetweenTheLanguages(ReportText text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.NotEqual(host.ReportText(text, "uk"), host.ReportText(text, "en"));
    }

    /// <summary>FR-004, AC-014: the exact sheet names.</summary>
    [Theory]
    [InlineData(ReportText.MeetSheetSummary, "Зведення", "Summary")]
    [InlineData(ReportText.MeetSheetMeetings, "Зустрічі", "Meetings")]
    [InlineData(ReportText.MeetSheetStudents, "Участь студентів", "Student participation")]
    [InlineData(ReportText.MeetSheetMeeting, "Зустріч", "Meeting")]
    public async Task TheSheetNames_AreExact(ReportText text, string ukrainian, string english)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.Equal(ukrainian, host.ReportText(text, "uk"));
        Assert.Equal(english, host.ReportText(text, "en"));
    }

    /// <summary>US-033 OD-003: the Ukrainian label of the students sheet says «студент».</summary>
    [Fact]
    public async Task TheUkrainianStudentsSheet_SaysStudent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        Assert.Contains("студент", host.ReportText(ReportText.MeetSheetStudents, "uk"), StringComparison.Ordinal);
    }

    /// <summary>FR-015, api-design §5: the new message exists in both languages and differs.</summary>
    [Fact]
    public async Task TheMeetingMalformedMessage_ExistsInBothLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        const string key = "Export.Validation.MeetingMalformed";

        var ukrainian = host.Text(key, "uk");
        var english = host.Text(key, "en");

        Assert.False(string.IsNullOrWhiteSpace(ukrainian));
        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.NotEqual(key, ukrainian);
        Assert.NotEqual(key, english);
        Assert.NotEqual(ukrainian, english);
    }
}
