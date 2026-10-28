using System.Globalization;
using System.Text.RegularExpressions;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-028 spec FR-004, FR-007 (entity model §2.1, §2.2): maps the report of the page to a format-neutral
/// <see cref="Workbook"/> — "Grading" then "Lesson topics", each with the header block, then its table or the empty
/// state. Every cell holds what the screen shows for it; nothing of the report is recomputed (spec FR-003). Program text
/// comes from <see cref="IReportTexts"/>; Google data and text a school wrote are copied as they are.
/// </summary>
public static partial class ReportWorkbookMapper
{
    /// <summary>The header block takes rows 0 … 3 and an empty row 4; the table or the empty state begins here.</summary>
    public const int TableRowIndex = 5;

    private const string PointsFormat = "0.############################";

    public static Workbook Map(Report report, IReportTexts texts, CultureInfo uiCulture, PageOrientation orientation)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(uiCulture);

        var teachers = TeachersText(report.Header, texts);

        return new Workbook(
            [Grading(report, texts, uiCulture, teachers), LessonTopics(report, texts, uiCulture, teachers)],
            orientation,
            DateFormat(uiCulture));
    }

    /// <summary>Spec FR-004.2: the teachers as the header shows them, joined with ", " (also US-043 spec FR-005.4).</summary>
    internal static string TeachersText(ReportHeader header, IReportTexts texts) =>
        string.Join(
            ", ",
            header.Teachers.Select(t => t.NameKind == ReportNameKind.Unnamed ? texts.Get(ReportText.TeacherUnnamed) : t.DisplayName));

    /// <summary>Spec FR-004.2: the course name and section as the header shows them (also US-043 spec FR-005.2).</summary>
    internal static string CourseText(ReportHeader header) =>
        string.IsNullOrEmpty(header.CourseSection) ? header.CourseName : header.CourseName + " — " + header.CourseSection;

    /// <summary>Spec FR-004.4: a student's displayed name (also US-043 spec FR-005.2).</summary>
    internal static string StudentText(PersonName student, IReportTexts texts) =>
        student.NameKind == ReportNameKind.Unnamed
            ? texts.Get(ReportText.StudentUnnamed)
            : student.DisplayName ?? string.Empty;

    /// <summary>US-060 spec FR-007.4, FR-008.2: the name cell keeps the name; a leaver's carries the "left" note.</summary>
    internal static WorkbookCell StudentCell(GradingRow row, IReportTexts texts, CultureInfo culture) =>
        WorkbookCell.OfText(StudentText(row.Student, texts)) with { Note = LeftText(row.Left, texts, culture) };

    /// <summary>US-060 spec FR-003.1, FR-007.1, FR-008.3: the course's header line, or null when it has none.</summary>
    internal static string? NoticeText(CourseDataNotice? notice, IReportTexts texts, CultureInfo culture) => notice switch
    {
        null => null,
        { Kind: CourseDataNoticeKind.NotInClassroom, Since: { } since } =>
            string.Format(culture, texts.Get(ReportText.DataHeaderNotInClassroomSince), since.ToString("d", culture)),
        { Since: { } since } =>
            string.Format(culture, texts.Get(ReportText.DataHeaderNotUpdatedSince), since.ToString("d", culture)),
        _ => texts.Get(ReportText.DataHeaderNotUpdated),
    };

    /// <summary>
    /// US-063 spec FR-006.2, VR-004, I-5: the note of a course-name cell — the existing note first when there is one, then
    /// "generated" and "data from Google as of", one per line; null when there is neither.
    /// </summary>
    internal static string? CourseNote(string? existing, CourseDataStamp? stamp, IReportTexts texts, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(existing))
        {
            lines.Add(existing);
        }

        if (stamp is not null)
        {
            var dataAsOf = stamp.DataAsOf is { } read ? DateTimeText(read, culture) : texts.Get(ReportText.DataAsOfUnknown);
            lines.Add(string.Format(culture, texts.Get(ReportText.DataGenerated), DateTimeText(stamp.GeneratedAt, culture)));
            lines.Add(string.Format(culture, texts.Get(ReportText.DataAsOf), dataAsOf));
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    /// <summary>US-063 spec FR-006.1, VR-003: a date-time as the screens show it — short date and hours:minutes.</summary>
    private static string DateTimeText(DateTimeOffset instant, CultureInfo culture) =>
        instant.ToString(culture.DateTimeFormat.ShortDatePattern + " HH:mm", culture);

    /// <summary>US-060 spec FR-005.2, FR-005.3, I-5: the item mark, no date.</summary>
    internal static string ItemMarkText(IReportTexts texts) => texts.Get(ReportText.DataItemNotInClassroom);

    /// <summary>US-060 spec FR-006.1: "left" with the last-seen date, or alone for a student known only through submissions.</summary>
    private static string? LeftText(LeftMark? left, IReportTexts texts, CultureInfo culture) => left switch
    {
        null => null,
        { LastSeenOn: { } date } => string.Format(culture, texts.Get(ReportText.DataLeftOn), date.ToString("d", culture)),
        _ => texts.Get(ReportText.DataLeft),
    };

    /// <summary>US-060 spec FR-007.3: one line per above-maximum value the cell shows — the grade, then the draft.</summary>
    private static string AboveMaximumNote(ReportAboveMaximum above, IReportTexts texts, CultureInfo culture)
    {
        var lines = new List<string>();
        var max = Points(above.MaxPoints, culture);
        if (above.Points is { } points)
        {
            lines.Add(string.Format(culture, texts.Get(ReportText.DataNotePoints), Points(points, culture), max));
        }

        if (above.DraftPoints is { } draft)
        {
            lines.Add(string.Format(culture, texts.Get(ReportText.DataNoteDraft), Points(draft, culture), max));
        }

        return string.Join('\n', lines);
    }

    /// <summary>Entity model §2.2: the UI culture's short date pattern with Excel's month letter.</summary>
    private static string DateFormat(CultureInfo culture) => culture.DateTimeFormat.ShortDatePattern.Replace('M', 'm');

    private static Worksheet Grading(Report report, IReportTexts texts, CultureInfo culture, string teachers)
    {
        var rows = HeaderBlock(report.Header, texts, culture, teachers);
        if (report.EmptyStateKey is not null || report.Grading is null)
        {
            rows.Add(Message(texts.Get(ReportText.NothingPublished)));
            return new Worksheet(texts.Get(ReportText.SheetGrading), rows, null, false);
        }

        var grading = report.Grading;
        if (grading.Rows.Count == 0)
        {
            rows.Add(Message(texts.Get(ReportText.NoStudents)));
            return new Worksheet(texts.Get(ReportText.SheetGrading), rows, null, false);
        }

        // Spec FR-004.3, I-3: title, date and the material marker of a column in one heading cell, as on screen.
        var heading = new List<WorkbookCell> { WorkbookCell.OfText(texts.Get(ReportText.StudentHeading), isHeading: true) };
        // US-060 spec FR-007.2: the "not in Classroom" item mark after a line break, last.
        heading.AddRange(grading.Columns.Select(c => WorkbookCell.OfText(
            c.Title + "\n" + c.LessonDate.ToString("d", culture)
            + (c.IsMaterial ? "\n" + texts.Get(ReportText.Material) : string.Empty)
            + (c.NotInClassroom ? "\n" + ItemMarkText(texts) : string.Empty),
            isHeading: true)));
        rows.Add(new WorksheetRow(heading));

        foreach (var row in grading.Rows)
        {
            var cells = new List<WorkbookCell>
            {
                StudentCell(row, texts, culture),
            };
            cells.AddRange(row.Cells.Select(c => GradingCell(c, texts, culture)));
            rows.Add(new WorksheetRow(cells));
        }

        return new Worksheet(texts.Get(ReportText.SheetGrading), rows, TableRowIndex, true);
    }

    private static Worksheet LessonTopics(Report report, IReportTexts texts, CultureInfo culture, string teachers)
    {
        var rows = HeaderBlock(report.Header, texts, culture, teachers);
        if (report.EmptyStateKey is not null || report.LessonTopics is null)
        {
            rows.Add(Message(texts.Get(ReportText.NothingPublished)));
            return new Worksheet(texts.Get(ReportText.SheetLessonTopics), rows, null, false);
        }

        rows.Add(new WorksheetRow(
            new[]
            {
                ReportText.TopicDate, ReportText.TopicTitle, ReportText.TopicHours,
                ReportText.TopicTeacher, ReportText.TopicIndependentWork, ReportText.TopicSignature,
            }
            .Select(t => WorkbookCell.OfText(texts.Get(t), isHeading: true))
            .ToList()));

        // Spec FR-004.4, I-4: a real date, the title, the hours as a number, the teachers; two empty columns.
        foreach (var topic in report.LessonTopics.Rows)
        {
            rows.Add(new WorksheetRow(
            [
                WorkbookCell.OfDate(topic.LessonDate),
                WorkbookCell.OfText(topic.NotInClassroom ? topic.Title + "\n" + ItemMarkText(texts) : topic.Title),
                WorkbookCell.OfNumber(topic.Hours),
                WorkbookCell.OfText(teachers),
                WorkbookCell.Empty(),
                WorkbookCell.Empty(),
            ]));
        }

        return new Worksheet(texts.Get(ReportText.SheetLessonTopics), rows, TableRowIndex, false);
    }

    /// <summary>Spec FR-004.2, I-2: template, course, period, teachers — each after its label — and one empty row.</summary>
    private static List<WorksheetRow> HeaderBlock(ReportHeader header, IReportTexts texts, CultureInfo culture, string teachers)
    {
        var template = header.TemplateIsBuiltIn ? texts.Get(ReportText.BuiltInTemplateName) : header.TemplateName ?? string.Empty;
        var course = CourseText(header);
        var period = header.From.ToString("d", culture) + " – " + header.To.ToString("d", culture);

        // US-060 spec FR-007.1, I-3: the header line in the third cell of the Course row, so the table does not move.
        // US-063 spec FR-004.1, FR-004.2: both lines in a note on the course-name cell of each sheet, never printed.
        var courseRow = new WorksheetRow(
        [
            WorkbookCell.OfText(texts.Get(ReportText.HeaderCourse), isHeading: true),
            WorkbookCell.OfText(course) with { Note = CourseNote(null, header.Stamp, texts, culture) },
        ]);
        if (NoticeText(header.Notice, texts, culture) is { } notice)
        {
            courseRow = new WorksheetRow([.. courseRow.Cells, WorkbookCell.OfText(notice)]);
        }

        return
        [
            Labelled(texts.Get(ReportText.HeaderTemplate), template),
            courseRow,
            Labelled(texts.Get(ReportText.HeaderPeriod), period),
            Labelled(texts.Get(ReportText.HeaderTeachers), teachers),
            new WorksheetRow([]),
        ];
    }

    private static WorksheetRow Labelled(string label, string value) =>
        new([WorkbookCell.OfText(label, isHeading: true), WorkbookCell.OfText(value)]);

    private static WorksheetRow Message(string text) => new([WorkbookCell.OfText(text)]);

    /// <summary>
    /// Spec FR-004.5, OD-003 a: a whole-number scale label alone is a number; an empty cell with nothing beside it is
    /// empty; anything else is the screen's text — main part, draft, late mark, turn-in date — one part per line.
    /// </summary>
    internal static WorkbookCell GradingCell(ReportCell cell, IReportTexts texts, CultureInfo culture)
    {
        var value = GradingValue(cell, texts, culture);

        // US-060 spec FR-007.3: an above-maximum value keeps the cell's value, gains the fill and the raw-points note.
        return cell.AboveMaximum is { } above
            ? value with { Highlighted = true, Note = AboveMaximumNote(above, texts, culture) }
            : value;
    }

    private static WorkbookCell GradingValue(ReportCell cell, IReportTexts texts, CultureInfo culture)
    {
        var alone = cell.DraftGrade is null && cell.Late is null && cell.TurnedInOn is null;
        if (alone && cell.Content == ReportCellContent.Empty)
        {
            return WorkbookCell.Empty();
        }

        if (alone
            && cell.Content == ReportCellContent.Grade
            && cell.Grade is { Kind: ReportGradeKind.ScaleLabel, Label: { } label }
            && WholeNumber().IsMatch(label))
        {
            return WorkbookCell.OfNumber(decimal.Parse(label, NumberStyles.None, CultureInfo.InvariantCulture));
        }

        var parts = new List<string>();
        switch (cell.Content)
        {
            case ReportCellContent.Grade:
                parts.Add(GradeText(cell.Grade, culture));
                break;
            case ReportCellContent.Mark when cell.Mark is { } mark:
                parts.Add(MarkText(mark, texts));
                break;
            case ReportCellContent.RawState:
                parts.Add(cell.RawState ?? string.Empty);
                break;
        }

        if (cell.DraftGrade is { } draft)
        {
            parts.Add(texts.Get(ReportText.Draft) + " " + GradeText(draft, culture));
        }

        if (cell.Late is { } late)
        {
            parts.Add(MarkText(late, texts));
        }

        if (cell.TurnedInOn is { } turnedInOn)
        {
            parts.Add(texts.Get(ReportText.TurnedInOn) + " " + turnedInOn.ToString("d", culture));
        }

        return WorkbookCell.OfText(string.Join('\n', parts));
    }

    /// <summary>US-027 spec FR-015: a scale label as written, or the raw points over the maximum, trailing zeros dropped.</summary>
    private static string GradeText(ReportGrade? grade, CultureInfo culture) =>
        grade is null
            ? string.Empty
            : grade.Kind == ReportGradeKind.ScaleLabel
                ? grade.Label ?? string.Empty
                : Points(grade.Points, culture) + " / " + Points(grade.MaxPoints, culture);

    private static string Points(decimal? points, CultureInfo culture) => points?.ToString(PointsFormat, culture) ?? string.Empty;

    private static string MarkText(ReportCellMark mark, IReportTexts texts) =>
        mark.Kind == ReportCellMarkKind.Own ? mark.Text ?? string.Empty : texts.ProgramMark(mark.ProgramKey ?? string.Empty);

    /// <summary>
    /// Entity model §2.1: no leading zero, so the number cell displays exactly the screen's text. <c>\z</c>, not <c>$</c>,
    /// so a label ending in a line break stays text.
    /// </summary>
    [GeneratedRegex(@"^(0|[1-9][0-9]{0,14})\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WholeNumber();
}
