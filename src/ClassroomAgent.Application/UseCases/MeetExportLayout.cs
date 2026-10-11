using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-044 spec FR-004, FR-007: what the two Meet workbooks share — the request's orientation, the header block's parts,
/// a person as two cells, minutes as the pages compute them, and the UI language's Excel formats. Every program text
/// comes through <see cref="IReportTexts"/>; Google data are written as stored.
/// </summary>
internal static class MeetExportLayout
{
    /// <summary>The <c>orientation</c> request values (spec VR-003), case-sensitive and not trimmed.</summary>
    public const string Portrait = "portrait";

    public const string Landscape = "landscape";

    /// <summary>Spec FR-005.1: the Summary's average number of students, one decimal place as on screen.</summary>
    public const string OneDecimal = "0.0";

    /// <summary>Spec VR-003: absent is landscape; otherwise exactly one of the two values.</summary>
    public static bool TryReadOrientation(string? value, out PageOrientation orientation)
    {
        orientation = PageOrientation.Landscape;
        switch (value)
        {
            case null or Landscape:
                return true;
            case Portrait:
                orientation = PageOrientation.Portrait;
                return true;
            default:
                return false;
        }
    }

    public static string OrientationCode(PageOrientation orientation) =>
        orientation == PageOrientation.Portrait ? Portrait : Landscape;

    /// <summary>US-033 I-5: seconds as whole minutes, half up — the pages' own rounding.</summary>
    public static decimal Minutes(long seconds) => (seconds + 30) / 60;

    /// <summary>A workbook with the UI language's date, time and date-time formats (spec FR-007).</summary>
    public static Workbook Workbook(IReadOnlyList<Worksheet> sheets, PageOrientation orientation, CultureInfo culture)
    {
        var date = culture.DateTimeFormat.ShortDatePattern.Replace('M', 'm');
        return new Workbook(sheets, orientation, date)
        {
            TimeFormat = "hh:mm",
            DateTimeFormat = date + " hh:mm",
        };
    }

    public static WorksheetRow Row(params WorkbookCell[] cells) => new(cells);

    public static WorksheetRow EmptyRow() => new([]);

    public static WorkbookCell Label(IReportTexts texts, ReportText text) => WorkbookCell.OfText(texts.Get(text), isHeading: true);

    public static WorkbookCell Heading(IReportTexts texts, ReportText text) => WorkbookCell.OfText(texts.Get(text), isHeading: true);

    public static WorkbookCell TextOrEmpty(string? text) =>
        string.IsNullOrEmpty(text) ? WorkbookCell.Empty() : WorkbookCell.OfText(text);

    public static WorkbookCell Number(decimal value) => WorkbookCell.OfNumber(value);

    /// <summary>Spec FR-007 "Persons" (OD-013): the name, empty when there is none; "unnamed" only with no email either.</summary>
    public static IEnumerable<WorkbookCell> Person(PersonRef person, IReportTexts texts)
    {
        var name = person.FullName ?? (person.Email is null ? texts.Get(ReportText.MeetPersonUnnamed) : null);
        yield return TextOrEmpty(name);
        yield return TextOrEmpty(person.Email);
    }

    /// <summary>Spec FR-004 "Exported": the instant of the export in the school's zone, as a date-time cell.</summary>
    public static WorksheetRow ExportedRow(IReportTexts texts, DateTime exportedLocal) =>
        Row(Label(texts, ReportText.MeetHeaderExported), WorkbookCell.OfDateTime(exportedLocal));

    /// <summary>
    /// Spec FR-004 row 7: the delay notice of US-033 FR-012 and the time Meet meetings are loaded up to — formatted as on
    /// screen — or "not loaded yet"; one text cell.
    /// </summary>
    public static WorksheetRow DelayRow(IReportTexts texts, DateTime? meetLoadedUpTo, CultureInfo culture)
    {
        var tail = meetLoadedUpTo is { } loaded
            ? texts.Get(ReportText.MeetDelayLoadedUpTo) + " "
              + loaded.ToString(culture.DateTimeFormat.ShortDatePattern + " HH:mm", culture)
            : texts.Get(ReportText.MeetDelayNotLoaded);
        return Row(WorkbookCell.OfText(texts.Get(ReportText.MeetDelayNotice) + " " + tail));
    }

    /// <summary>US-033 I-7: an end on the start's day is a time; otherwise a date-time.</summary>
    public static WorkbookCell End(DateTime start, DateTime end) =>
        end.Date == start.Date ? WorkbookCell.OfTime(TimeOnly.FromDateTime(end)) : WorkbookCell.OfDateTime(end);

    public static WorkbookCell Start(DateTime start) => WorkbookCell.OfTime(TimeOnly.FromDateTime(start));

    public static WorkbookCell Day(DateTime start) => WorkbookCell.OfDate(DateOnly.FromDateTime(start));

    public static ReportText MarkText(MeetParticipantMark mark) => mark switch
    {
        MeetParticipantMark.Organizer => ReportText.MeetMarkOrganizer,
        MeetParticipantMark.CourseTeacher => ReportText.MeetMarkCourseTeacher,
        MeetParticipantMark.CourseStudent => ReportText.MeetMarkCourseStudent,
        MeetParticipantMark.NotOnCourseList => ReportText.MeetMarkNotOnCourseList,
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
    };
}
