namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-038 <c>ErasureConfirmationPageModel</c> (spec FR-003, OD-001 a, I-8): mapped in Application (AD-8), no
/// Google user id. <see cref="MarkError"/> is set only on the VR-002 re-render.
/// </summary>
public sealed record ErasureConfirmationPageModel(
    long ParticipantId,
    string? FullName,
    string? Email,
    IReadOnlyList<ErasureCourseEntry> Courses,
    int Submissions,
    int Grades,
    int MeetParticipations,
    int OrganizedMeetings,
    int OwnedCourses,
    bool NoEmailNotice,
    bool ReadOnlyNotice,
    ErasureMarkError? MarkError);
