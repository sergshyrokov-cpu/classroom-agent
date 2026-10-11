using ClassroomAgent.Application.Localization;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Models.Dtos;
using Microsoft.Extensions.Localization;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// US-028 entity model §3.2: the program text of an export, through the installation's translations, in the current UI culture.
/// </summary>
public sealed class LocalizedReportTexts(IStringLocalizer<SharedResource> localizer) : IReportTexts
{
    public string Get(ReportText text)
    {
        var key = text switch
        {
            ReportText.SheetGrading => JournalExportTextKeys.SheetGrading,
            ReportText.SheetLessonTopics => JournalExportTextKeys.SheetLessonTopics,
            ReportText.HeaderTemplate => JournalExportTextKeys.HeaderTemplate,
            ReportText.HeaderCourse => JournalExportTextKeys.HeaderCourse,
            ReportText.HeaderPeriod => ReportTemplateTextKeys.ReportPeriod,
            ReportText.HeaderTeachers => ReportTemplateTextKeys.ReportTeachers,
            ReportText.BuiltInTemplateName => ReportTemplateTextKeys.BuiltInName,
            ReportText.StudentHeading => JournalTextKeys.StudentHeader,
            ReportText.Material => JournalTextKeys.Material,
            ReportText.Draft => JournalTextKeys.Draft,
            ReportText.TurnedInOn => JournalTextKeys.TurnedInOn,
            ReportText.StudentUnnamed => JournalTextKeys.Unnamed,
            ReportText.TeacherUnnamed => ReportTemplateTextKeys.TeacherUnnamed,
            ReportText.TopicDate => ReportTemplateTextKeys.ReportDate,
            ReportText.TopicTitle => ReportTemplateTextKeys.ReportTopic,
            ReportText.TopicHours => ReportTemplateTextKeys.ReportHours,
            ReportText.TopicTeacher => ReportTemplateTextKeys.ReportTeacher,
            ReportText.TopicIndependentWork => ReportTemplateTextKeys.ReportIndependentWork,
            ReportText.TopicSignature => ReportTemplateTextKeys.ReportSignature,
            ReportText.NothingPublished => ReportTemplateTextKeys.Empty(ReportEmptyStateKey.NothingPublished),
            ReportText.NoStudents => ReportTemplateTextKeys.NoStudents,
            ReportText.CourseFallback => JournalExportTextKeys.CourseFallback,
            ReportText.MeetSheetSummary => MeetExportTextKeys.SheetSummary,
            ReportText.MeetSheetMeetings => MeetExportTextKeys.SheetMeetings,
            ReportText.MeetSheetStudents => MeetExportTextKeys.SheetStudents,
            ReportText.MeetSheetMeeting => MeetExportTextKeys.SheetMeeting,
            ReportText.MeetHeaderCourse => JournalExportTextKeys.HeaderCourse,
            ReportText.MeetHeaderPeriod => MeetExportTextKeys.HeaderPeriod,
            ReportText.MeetHeaderMeeting => MeetExportTextKeys.HeaderMeeting,
            ReportText.MeetHeaderDuration => MeetExportTextKeys.HeaderDuration,
            ReportText.MeetHeaderMeetingCode => MeetExportTextKeys.HeaderMeetingCode,
            ReportText.MeetHeaderOrganizer => MeetExportTextKeys.HeaderOrganizer,
            ReportText.MeetHeaderExported => MeetExportTextKeys.HeaderExported,
            ReportText.MeetSummaryIndicator => MeetExportTextKeys.SummaryIndicator,
            ReportText.MeetSummaryValue => MeetExportTextKeys.SummaryValue,
            ReportText.MeetSummaryMeetingCount => MeetExportTextKeys.SummaryMeetingCount,
            ReportText.MeetSummaryTotalMinutes => MeetExportTextKeys.SummaryTotalMinutes,
            ReportText.MeetSummaryAverageMinutes => MeetExportTextKeys.SummaryAverageMinutes,
            ReportText.MeetSummaryAverageStudents => MeetExportTextKeys.SummaryAverageStudents,
            ReportText.MeetColumnEmail => MeetExportTextKeys.ColumnEmail,
            ReportText.MeetColumnOrganizerEmail => MeetExportTextKeys.ColumnOrganizerEmail,
            ReportText.MeetColumnOrganizerMark => MeetExportTextKeys.ColumnOrganizerMark,
            ReportText.MeetColumnPerson => MeetExportTextKeys.ColumnPerson,
            ReportText.MeetColumnMark => MeetExportTextKeys.ColumnMark,
            ReportText.MeetColumnMinutes => MeetReportsTextKeys.StudentsMinutes,
            ReportText.MeetColumnOfMeetingMinutes => MeetExportTextKeys.ColumnOfMeetingMinutes,
            ReportText.MeetOtherParticipants => MeetReportsTextKeys.MeetingOtherParticipants,
            ReportText.MeetColumnDate => MeetReportsTextKeys.MeetingsDate,
            ReportText.MeetColumnStart => MeetReportsTextKeys.MeetingsStart,
            ReportText.MeetColumnEnd => MeetReportsTextKeys.MeetingsEnd,
            ReportText.MeetColumnDuration => MeetReportsTextKeys.MeetingsDuration,
            ReportText.MeetColumnOrganizer => MeetReportsTextKeys.MeetingsOrganizer,
            ReportText.MeetColumnCourseTeachers => MeetReportsTextKeys.MeetingsCourseTeachers,
            ReportText.MeetColumnCourseStudents => MeetReportsTextKeys.MeetingsCourseStudents,
            ReportText.MeetColumnNotOnCourseList => MeetReportsTextKeys.MeetingsNotOnCourseList,
            ReportText.MeetColumnOtherParticipants => MeetReportsTextKeys.MeetingsOtherParticipants,
            ReportText.MeetColumnStudent => MeetReportsTextKeys.StudentsStudent,
            ReportText.MeetColumnMeetings => MeetReportsTextKeys.StudentsMeetings,
            ReportText.MeetMarkOrganizer => MeetReportsTextKeys.Mark(MeetParticipantMark.Organizer),
            ReportText.MeetMarkCourseTeacher => MeetReportsTextKeys.Mark(MeetParticipantMark.CourseTeacher),
            ReportText.MeetMarkCourseStudent => MeetReportsTextKeys.Mark(MeetParticipantMark.CourseStudent),
            ReportText.MeetMarkNotOnCourseList => MeetReportsTextKeys.Mark(MeetParticipantMark.NotOnCourseList),
            ReportText.MeetMarkNotCourseTeacher => MeetReportsTextKeys.MarkNotCourseTeacher,
            ReportText.MeetPersonUnnamed => MeetReportsTextKeys.PersonUnnamed,
            ReportText.MeetDelayNotice => MeetReportsTextKeys.DelayNotice,
            ReportText.MeetDelayLoadedUpTo => MeetReportsTextKeys.DelayNoticeLoadedUpTo,
            ReportText.MeetDelayNotLoaded => MeetReportsTextKeys.DelayNoticeNotLoaded,
            ReportText.MeetEmptyNoMeetings => MeetReportsTextKeys.EmptyNoMeetings,
            ReportText.MeetEmptyNoStudents => MeetReportsTextKeys.EmptyNoStudents,
            _ => throw new ArgumentOutOfRangeException(nameof(text), text, null),
        };

        return localizer[key];
    }

    public string ProgramMark(string programKey) => localizer[ReportTemplateTextKeys.ProgramMark(programKey)];
}
