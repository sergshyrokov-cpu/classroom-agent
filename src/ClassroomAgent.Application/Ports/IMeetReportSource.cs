using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>The Meet reports' reads (US-033 db-design §3, entity model §3). Read-only, untracked; nothing logged but counts.</summary>
public interface IMeetReportSource
{
    /// <summary>R6 — the course's name, or null when no course has this id.</summary>
    Task<string?> GetCourseNameAsync(long courseId, CancellationToken cancellationToken);

    /// <summary>R1 + R2 — meetings of the course with start in [start, endExclusive), each with its connections.</summary>
    Task<IReadOnlyList<MeetReportMeetingRecord>> GetMeetingsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);

    /// <summary>R3 — one meeting with its connections when it is a meeting of the course; otherwise null.</summary>
    Task<MeetReportMeetingRecord?> GetMeetingOfCourseAsync(long courseId, long meetingId, CancellationToken cancellationToken);

    /// <summary>R4 — every membership of the course, any role, with its participant's email and full name.</summary>
    Task<IReadOnlyList<MeetReportMemberRecord>> GetCourseMembersAsync(long courseId, CancellationToken cancellationToken);

    /// <summary>R5 — one record per requested lower-cased email that matches a participant (db-design §4).</summary>
    Task<IReadOnlyList<MeetReportPersonRecord>> GetPeopleAsync(
        IReadOnlyCollection<string> lowerCaseEmails,
        CancellationToken cancellationToken);
}
