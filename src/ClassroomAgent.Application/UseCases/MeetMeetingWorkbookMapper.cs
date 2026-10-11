using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;
using static ClassroomAgent.Application.UseCases.MeetExportLayout;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-044 spec FR-004, FR-006, FR-007 (entity model §2.3): report 4 of the meeting page as a one-sheet workbook — the
/// header block, the identified people with mark, N and M minutes, and below the table, outside its autofilter, the
/// "Other participants" line with a count and minutes only (spec I-5, S-12).
/// </summary>
internal static class MeetMeetingWorkbookMapper
{
    /// <summary>Spec FR-004: seven header rows and one empty row; the table header is the ninth row.</summary>
    private const int TableHeaderRowIndex = 8;

    public static Workbook Map(
        MeetingDetail meeting,
        DateTime? meetLoadedUpTo,
        DateTime exportedLocal,
        IReportTexts texts,
        CultureInfo culture,
        PageOrientation orientation)
    {
        var meetingMinutes = Minutes(meeting.DurationSeconds);
        var rows = new List<WorksheetRow>
        {
            Row(Label(texts, ReportText.MeetHeaderCourse), WorkbookCell.OfText(meeting.CourseName)),
            Row(Label(texts, ReportText.MeetHeaderMeeting), Day(meeting.StartedAt), Start(meeting.StartedAt), End(meeting.StartedAt, meeting.EndedAt)),
            Row(Label(texts, ReportText.MeetHeaderDuration), Number(meetingMinutes)),
            Row(Label(texts, ReportText.MeetHeaderMeetingCode), WorkbookCell.OfText(meeting.MeetingCode)),
            Row(
            [
                Label(texts, ReportText.MeetHeaderOrganizer),
                .. Person(meeting.Organizer, texts),
                meeting.OrganizerNotCourseTeacher
                    ? WorkbookCell.OfText(texts.Get(ReportText.MeetMarkNotCourseTeacher))
                    : WorkbookCell.Empty(),
            ]),
            ExportedRow(texts, exportedLocal),
            DelayRow(texts, meetLoadedUpTo, culture),
            EmptyRow(),
            Row(
                Heading(texts, ReportText.MeetColumnPerson),
                Heading(texts, ReportText.MeetColumnEmail),
                Heading(texts, ReportText.MeetColumnMark),
                Heading(texts, ReportText.MeetColumnMinutes),
                Heading(texts, ReportText.MeetColumnOfMeetingMinutes)),
        };

        foreach (var line in meeting.People)
        {
            rows.Add(Row(
            [
                .. Person(line.Person, texts),
                WorkbookCell.OfText(texts.Get(MarkText(line.Mark))),
                Number(Minutes(line.Seconds)),
                Number(meetingMinutes),
            ]));
        }

        // Spec I-6: with nobody identified the table keeps its header row alone; the line still follows.
        var tableLastRowIndex = rows.Count - 1;
        rows.Add(EmptyRow());
        rows.Add(Row(
            Label(texts, ReportText.MeetOtherParticipants),
            Number(meeting.OtherParticipants.Count),
            Number(Minutes(meeting.OtherParticipants.TotalSeconds))));

        var sheet = new Worksheet(texts.Get(ReportText.MeetSheetMeeting), rows, TableHeaderRowIndex, RepeatAndFreezeFirstColumn: false)
        {
            AutoFilter = true,
            TableLastRowIndex = tableLastRowIndex,
        };
        return Workbook([sheet], orientation, culture);
    }
}
