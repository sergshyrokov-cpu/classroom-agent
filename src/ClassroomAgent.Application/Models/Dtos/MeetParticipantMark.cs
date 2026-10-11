namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Closed list (spec FR-005, v58); each maps to MeetReports.Mark.&lt;member&gt;.</summary>
public enum MeetParticipantMark
{
    Organizer,
    CourseTeacher,
    CourseStudent,
    NotOnCourseList,
}
