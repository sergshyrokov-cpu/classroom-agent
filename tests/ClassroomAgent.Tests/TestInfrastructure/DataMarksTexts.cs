using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-060 spec FR-009: the mark, label and note texts the tests pin, Ukrainian and English, and the date format of
/// spec §4 preamble — the instant converted to the school's zone (BR-053, TC-8), shown with the language's short date
/// pattern. The Ukrainian texts are the spec's table verbatim (I-6; «вибув(-ла)» from HUMAN_SPEC_APPROVAL).
/// </summary>
public sealed record DataMarksTexts(
    string Language,
    string CourseNotInClassroomSince,
    string CourseNotVisibleSince,
    string NoTeacher,
    string HeaderNotInClassroomSince,
    string HeaderNotUpdatedSince,
    string HeaderNotUpdated,
    string ItemNotInClassroom,
    string AboveMaximum,
    string NoteGrade,
    string NoteDraft,
    string LeftOn,
    string Left)
{
    public static DataMarksTexts Uk { get; } = new(
        "uk",
        "немає в Classroom з {0}",
        "не видно технічному акаунту з {0}",
        "викладача немає в Classroom",
        "Курсу немає в Classroom з {0}",
        "Дані не оновлювалися з {0}",
        "Дані не оновлювалися",
        "немає в Classroom",
        "більше максимуму",
        "Бали: {0} з {1}",
        "Чернетка: {0} з {1}",
        "вибув(-ла) {0}",
        "вибув(-ла)");

    public static DataMarksTexts En { get; } = new(
        "en",
        "not in Classroom since {0}",
        "not visible to the technical account since {0}",
        "no teacher in Classroom",
        "Course not in Classroom since {0}",
        "Data not updated since {0}",
        "Data not updated",
        "not in Classroom",
        "above maximum",
        "Points: {0} of {1}",
        "Draft: {0} of {1}",
        "left {0}",
        "left");

    public static DataMarksTexts Of(string language) => language == "en" ? En : Uk;

    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language);

    /// <summary>Spec §4 preamble, VR-004: the calendar date of the instant in Kyiv, in this language's short date pattern.</summary>
    public string Date(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, JournalTestData.Kyiv).DateTime).ToString("d", Culture);

    public string With(string format, DateTimeOffset instant) => string.Format(Culture, format, Date(instant));

    public string Note(string format, int points, int maximum) => string.Format(Culture, format, points, maximum);
}
