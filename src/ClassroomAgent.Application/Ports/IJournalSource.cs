using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The journal's reads (US-025 entity model §2, db-design §2): four fixed queries, none of which writes, opens a
/// transaction or reaches Google. Every result is unordered — the orders of spec FR-002, FR-004 and FR-005 are applied
/// by the caller.
/// </summary>
public interface IJournalSource
{
    /// <summary>Q1 — every stored course.</summary>
    Task<IReadOnlyList<JournalCourseRecord>> GetCoursesAsync(CancellationToken cancellationToken);

    /// <summary>Q2 — items of the course with start &lt;= ItemDate &lt; endExclusive; both bounds UTC.</summary>
    Task<IReadOnlyList<JournalItemRecord>> GetItemsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);

    /// <summary>Q3 — every membership of the course with role Student, with its participant.</summary>
    Task<IReadOnlyList<JournalMemberRecord>> GetStudentMembersAsync(long courseId, CancellationToken cancellationToken);

    /// <summary>Q4 — every submission to an item of the course dated in the period; not deduplicated (OD-008).</summary>
    Task<IReadOnlyList<JournalSubmissionRecord>> GetSubmissionsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);
}
