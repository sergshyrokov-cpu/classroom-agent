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

        var teachers = string.Join(
            ", ",
            report.Header.Teachers.Select(t => t.NameKind == ReportNameKind.Unnamed ? texts.Get(ReportText.TeacherUnnamed) : t.DisplayName));

        return new Workbook(
            [Grading(report, texts, uiCulture, teachers), LessonTopics(report, texts, uiCulture, teachers)],
            orientation,
            DateFormat(uiCulture));
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
        heading.AddRange(grading.Columns.Select(c => WorkbookCell.OfText(
            c.Title + "\n" + c.LessonDate.ToString("d", culture) + (c.IsMaterial ? "\n" + texts.Get(ReportText.Material) : string.Empty),
            isHeading: true)));
        rows.Add(new WorksheetRow(heading));

        foreach (var row in grading.Rows)
        {
            var cells = new List<WorkbookCell>
            {
                WorkbookCell.OfText(row.Student.NameKind == ReportNameKind.Unnamed
                    ? texts.Get(ReportText.StudentUnnamed)
                    : row.Student.DisplayName ?? string.Empty),
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
                WorkbookCell.OfText(topic.Title),
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
        var course = string.IsNullOrEmpty(header.CourseSection) ? header.CourseName : header.CourseName + " — " + header.CourseSection;
        var period = header.From.ToString("d", culture) + " – " + header.To.ToString("d", culture);

        return
        [
            Labelled(texts.Get(ReportText.HeaderTemplate), template),
            Labelled(texts.Get(ReportText.HeaderCourse), course),
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
    private static WorkbookCell GradingCell(ReportCell cell, IReportTexts texts, CultureInfo culture)
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
