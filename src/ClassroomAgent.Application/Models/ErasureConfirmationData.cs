namespace ClassroomAgent.Application.Models;

/// <summary>US-038 spec FR-003: what the confirmation page shows, read from the database.</summary>
public sealed record ErasureConfirmationData(
    long ParticipantId,
    string? FullName,
    string? Email,
    IReadOnlyList<ErasureCourseRecord> Courses,
    int Submissions,
    int Grades,
    int MeetParticipations,
    int OrganizedMeetings,
    int OwnedCourses);
