using ClassroomAgent.Application.UseCases;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-044 AC-008 (spec FR-008.1; entity model §2.4): the Meet export file names are the course part plus the period or
/// the meeting start, the course part cleaned by exactly the rule of the journal export.
/// </summary>
public sealed class MeetExportFileNameTests
{
    private const string Fallback = "course";

    private const string JournalSuffix = " 2026-09-01–2026-09-30.xlsx";

    private static readonly DateOnly From = new(2026, 9, 1);

    private static readonly DateOnly To = new(2026, 9, 30);

    public static TheoryData<string> CourseNames() =>
    [
        "Алгебра 7/А",
        "  a   b  ",
        "a\\b:c*d?e\"f<g>h|i",
        new string('x', 150),
        string.Empty,
        "   ",
        "...",
        "=cmd",
    ];

    /// <summary>FR-008.1: the period form, Cyrillic kept, an en dash between the dates.</summary>
    [Fact]
    public void ThePeriodForm_IsMeetCourseAndPeriod_WithAnEnDash() =>
        Assert.Equal(
            "Meet Алгебра 7-А 2026-09-01–2026-09-30.xlsx",
            MeetExportFileName.BuildForPeriod("Алгебра 7-А", From, To, Fallback));

    /// <summary>FR-008.1: the meeting form, date and local start with HH-mm.</summary>
    [Fact]
    public void TheMeetingForm_IsMeetCourseDateAndStart() =>
        Assert.Equal(
            "Meet Алгебра 7-А 2026-09-14 08-30.xlsx",
            MeetExportFileName.BuildForMeeting("Алгебра 7-А", new DateTime(2026, 9, 14, 8, 30, 0), Fallback));

    /// <summary>FR-008.1: hours and minutes are always two digits; the separator is a hyphen.</summary>
    [Theory]
    [InlineData(23, 5, "23-05")]
    [InlineData(0, 0, "00-00")]
    [InlineData(9, 7, "09-07")]
    public void TheStartTime_IsHhMm_WithLeadingZeros(int hour, int minute, string expected) =>
        Assert.Equal(
            $"Meet Алгебра 7-А 2026-09-14 {expected}.xlsx",
            MeetExportFileName.BuildForMeeting("Алгебра 7-А", new DateTime(2026, 9, 14, hour, minute, 0), Fallback));

    /// <summary>FR-008.1: a colon is not allowed in a Windows file name.</summary>
    [Fact]
    public void TheMeetingForm_NeverContainsAColon()
    {
        var name = MeetExportFileName.BuildForMeeting("a:b", new DateTime(2026, 9, 14, 8, 30, 0), Fallback);

        Assert.DoesNotContain(':', name);
    }

    /// <summary>FR-008.1: the course part of the period form is the journal rule's, whatever the name.</summary>
    [Theory]
    [MemberData(nameof(CourseNames))]
    public void ThePeriodForm_CoursePart_IsTheJournalRule(string courseName) =>
        Assert.Equal(
            "Meet " + JournalExportFileName.Build(courseName, From, To, Fallback),
            MeetExportFileName.BuildForPeriod(courseName, From, To, Fallback));

    /// <summary>FR-008.1: the course part of the meeting form is the journal rule's, whatever the name.</summary>
    [Theory]
    [MemberData(nameof(CourseNames))]
    public void TheMeetingForm_CoursePart_IsTheJournalRule(string courseName)
    {
        var journal = JournalExportFileName.Build(courseName, From, To, Fallback);
        Assert.EndsWith(JournalSuffix, journal, StringComparison.Ordinal);
        var coursePart = journal[..^JournalSuffix.Length];

        Assert.Equal(
            "Meet " + coursePart + " 2026-09-14 08-30.xlsx",
            MeetExportFileName.BuildForMeeting(courseName, new DateTime(2026, 9, 14, 8, 30, 0), Fallback));
    }
}
