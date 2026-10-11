namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Report 2, one Google meeting (spec FR-008). Date-times are local to the school's zone.</summary>
public sealed record MeetListRow(
    long MeetingId,
    DateTime StartedAt,
    DateTime EndedAt,
    long DurationSeconds,
    PersonRef Organizer,
    bool OrganizerNotCourseTeacher,
    int CourseTeachers,
    int CourseStudents,
    int NotOnCourseList,
    int OtherParticipants);
