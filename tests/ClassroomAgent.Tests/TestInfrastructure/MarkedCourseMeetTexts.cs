namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-061 spec FR-005.1, FR-006: the Meet line of a marked course — the mark, then the roster, joined by «; » — in
/// Ukrainian and English, verbatim from the spec's table. Dates follow <see cref="DataMarksTexts.Date"/>: the instant in
/// Kyiv, the language's short date pattern (spec §4 preamble, VR-003, TC-8).
/// </summary>
public sealed record MarkedCourseMeetTexts(
    string Language,
    string NotInClassroomSince,
    string NotVisibleSince,
    string RosterAsOf,
    string RosterUndated,
    string Separator)
{
    public static MarkedCourseMeetTexts Uk { get; } = new(
        "uk",
        "Курсу немає в Classroom з {0}",
        "Курс не видно технічному акаунту з {0}",
        "склад курсу — на {0}",
        "склад курсу — на дату останнього читання",
        "; ");

    public static MarkedCourseMeetTexts En { get; } = new(
        "en",
        "Course not in Classroom since {0}",
        "Course not visible to the technical account since {0}",
        "course membership as of {0}",
        "course membership as of the last read",
        "; ");

    public static MarkedCourseMeetTexts Of(string language) => language == "en" ? En : Uk;

    /// <summary>The part every roster line starts with, dated or not — absent from a page or file with no line.</summary>
    public string RosterPrefix => RosterAsOf[..RosterAsOf.IndexOf("{0}", StringComparison.Ordinal)].TrimEnd();

    private DataMarksTexts Dates => DataMarksTexts.Of(Language);

    /// <summary>The whole line of a course marked "not in Classroom" since <paramref name="since"/>.</summary>
    public string NotInClassroomLine(DateTimeOffset since, DateTimeOffset? lastRead) =>
        Dates.With(NotInClassroomSince, since) + Separator + Roster(lastRead);

    /// <summary>The whole line of a course marked "not visible to the technical account" since <paramref name="since"/>.</summary>
    public string NotVisibleLine(DateTimeOffset since, DateTimeOffset? lastRead) =>
        Dates.With(NotVisibleSince, since) + Separator + Roster(lastRead);

    /// <summary>OD-001 a: the roster part, undated when the last complete read is unknown.</summary>
    public string Roster(DateTimeOffset? lastRead) =>
        lastRead is { } read ? Dates.With(RosterAsOf, read) : RosterUndated;
}
