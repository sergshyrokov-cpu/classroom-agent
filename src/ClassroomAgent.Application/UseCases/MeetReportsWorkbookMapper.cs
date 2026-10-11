using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;
using static ClassroomAgent.Application.UseCases.MeetExportLayout;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-044 spec FR-004, FR-005, FR-007 (entity model §2.3): reports 1–3 of the page as the course workbook — Summary,
/// Meetings, Student participation, each with the header block, one empty row and its table (or empty-state line).
/// Values are those of the page's DTOs; nothing is recomputed except minutes, rounded as the page rounds them.
/// </summary>
internal static class MeetReportsWorkbookMapper
{
    /// <summary>Spec FR-004: four header rows and one empty row; the table header is the sixth row.</summary>
    private const int TableHeaderRowIndex = 5;

    public static Workbook Map(
        string courseName,
        DateOnly from,
        DateOnly to,
        MeetReports reports,
        DateTime exportedLocal,
        IReportTexts texts,
        CultureInfo culture,
        PageOrientation orientation)
    {
        List<WorksheetRow> Header() =>
        [
            Row(Label(texts, ReportText.MeetHeaderCourse), WorkbookCell.OfText(courseName)),
            Row(Label(texts, ReportText.MeetHeaderPeriod), WorkbookCell.OfDate(from), WorkbookCell.OfDate(to)),
            ExportedRow(texts, exportedLocal),
            DelayRow(texts, reports.DelayNotice.MeetLoadedUpTo, culture),
            EmptyRow(),
        ];

        return Workbook(
            [Summary(Header(), reports.Summary, texts), Meetings(Header(), reports.Meetings, texts), Students(Header(), reports.Students, texts)],
            orientation,
            culture);
    }

    /// <summary>Spec FR-005.1, I-2: an Indicator / Value table of four rows, with its autofilter.</summary>
    private static Worksheet Summary(List<WorksheetRow> rows, MeetSummary summary, IReportTexts texts)
    {
        rows.Add(Row(Heading(texts, ReportText.MeetSummaryIndicator), Heading(texts, ReportText.MeetSummaryValue)));
        rows.Add(Row(TextOf(texts, ReportText.MeetSummaryMeetingCount), Number(summary.MeetingCount)));
        rows.Add(Row(TextOf(texts, ReportText.MeetSummaryTotalMinutes), Number(Minutes(summary.TotalDurationSeconds))));
        rows.Add(Row(TextOf(texts, ReportText.MeetSummaryAverageMinutes), Number(Minutes(summary.AverageDurationSeconds))));
        rows.Add(Row(
            TextOf(texts, ReportText.MeetSummaryAverageStudents),
            WorkbookCell.OfNumber(summary.AverageStudentsPerMeeting, OneDecimal)));
        return Table(texts.Get(ReportText.MeetSheetSummary), rows);
    }

    /// <summary>Spec FR-005.2: the eleven columns of report 2, the organizer as name, email and mark (OD-013).</summary>
    private static Worksheet Meetings(List<WorksheetRow> rows, IReadOnlyList<MeetListRow> meetings, IReportTexts texts)
    {
        var name = texts.Get(ReportText.MeetSheetMeetings);
        if (meetings.Count == 0)
        {
            rows.Add(Row(WorkbookCell.OfText(texts.Get(ReportText.MeetEmptyNoMeetings))));
            return EmptyState(name, rows);
        }

        rows.Add(Row(
        [
            .. new[]
            {
                ReportText.MeetColumnDate, ReportText.MeetColumnStart, ReportText.MeetColumnEnd, ReportText.MeetColumnDuration,
                ReportText.MeetColumnOrganizer, ReportText.MeetColumnOrganizerEmail, ReportText.MeetColumnOrganizerMark,
                ReportText.MeetColumnCourseTeachers, ReportText.MeetColumnCourseStudents, ReportText.MeetColumnNotOnCourseList,
                ReportText.MeetColumnOtherParticipants,
            }.Select(t => Heading(texts, t)),
        ]));
        foreach (var meeting in meetings)
        {
            rows.Add(Row(
            [
                Day(meeting.StartedAt),
                Start(meeting.StartedAt),
                End(meeting.StartedAt, meeting.EndedAt),
                Number(Minutes(meeting.DurationSeconds)),
                .. Person(meeting.Organizer, texts),
                meeting.OrganizerNotCourseTeacher ? TextOf(texts, ReportText.MeetMarkNotCourseTeacher) : WorkbookCell.Empty(),
                Number(meeting.CourseTeachers),
                Number(meeting.CourseStudents),
                Number(meeting.NotOnCourseList),
                Number(meeting.OtherParticipants),
            ]));
        }

        return Table(name, rows);
    }

    /// <summary>Spec FR-005.3: Student, Email, Meetings, Minutes.</summary>
    private static Worksheet Students(List<WorksheetRow> rows, IReadOnlyList<StudentParticipationRow> students, IReportTexts texts)
    {
        var name = texts.Get(ReportText.MeetSheetStudents);
        if (students.Count == 0)
        {
            rows.Add(Row(WorkbookCell.OfText(texts.Get(ReportText.MeetEmptyNoStudents))));
            return EmptyState(name, rows);
        }

        rows.Add(Row(
            Heading(texts, ReportText.MeetColumnStudent),
            Heading(texts, ReportText.MeetColumnEmail),
            Heading(texts, ReportText.MeetColumnMeetings),
            Heading(texts, ReportText.MeetColumnMinutes)));
        foreach (var student in students)
        {
            rows.Add(Row([.. Person(student.Student, texts), Number(student.Meetings), Number(Minutes(student.TotalSeconds))]));
        }

        return Table(name, rows);
    }

    private static WorkbookCell TextOf(IReportTexts texts, ReportText text) => WorkbookCell.OfText(texts.Get(text));

    /// <summary>Spec FR-007: autofilter on the header row, header frozen and repeated on every printed page.</summary>
    private static Worksheet Table(string name, List<WorksheetRow> rows) =>
        new(name, rows, TableHeaderRowIndex, RepeatAndFreezeFirstColumn: false) { AutoFilter = true };

    /// <summary>Spec FR-007: a table replaced by an empty-state line has no autofilter, nothing to freeze.</summary>
    private static Worksheet EmptyState(string name, List<WorksheetRow> rows) =>
        new(name, rows, TableHeaderRowIndex: null, RepeatAndFreezeFirstColumn: false);
}
