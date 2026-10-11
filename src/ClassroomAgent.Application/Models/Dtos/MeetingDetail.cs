namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Report 4 (spec FR-010). Date-times are local to the school's zone.</summary>
public sealed record MeetingDetail(
    string CourseName,
    string MeetingCode,
    DateTime StartedAt,
    DateTime EndedAt,
    long DurationSeconds,
    PersonRef Organizer,
    bool OrganizerNotCourseTeacher,
    IReadOnlyList<MeetingPersonLine> People,
    MeetOtherParticipants OtherParticipants);
