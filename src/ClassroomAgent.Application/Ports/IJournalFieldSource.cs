using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The journal fields a report reads (US-027 spec FR-004; db-design §5.1): four fixed queries, none of which writes,
/// opens a transaction or reaches Google. Results are unordered; bounds are UTC; the lesson date is
/// <c>scheduledTime</c>, else <c>creationTime</c>, else the item date (OD-001 a, spec I-9).
/// </summary>
public interface IJournalFieldSource
{
    /// <summary>F1 — every stored course.</summary>
    Task<IReadOnlyList<JournalCourseRecord>> GetCoursesAsync(CancellationToken cancellationToken);

    /// <summary>F2 — items of the course with start &lt;= lesson date &lt; endExclusive.</summary>
    Task<IReadOnlyList<JournalLessonRecord>> GetLessonsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);

    /// <summary>F3 — every membership of the course, both roles, with its participant.</summary>
    Task<IReadOnlyList<JournalCourseMemberRecord>> GetMembersAsync(long courseId, CancellationToken cancellationToken);

    /// <summary>F4 — every submission to an item F2 returns for the same arguments; not deduplicated.</summary>
    Task<IReadOnlyList<JournalSubmissionRecord>> GetLessonSubmissionsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);
}
