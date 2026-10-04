using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The journal's four reads (US-025 db-design §2, entity model §2). One database command each, every one without
/// change tracking; no order, no deduplication — both belong to the use case. Never saves, never opens a
/// transaction (spec FR-013, FR-014).
/// </summary>
public sealed class JournalSource(ClassroomAgentDbContext db) : IJournalSource
{
    /// <summary>Q1 — every stored course, unordered.</summary>
    public async Task<IReadOnlyList<JournalCourseRecord>> GetCoursesAsync(CancellationToken cancellationToken) =>
        await db.Courses
            .AsNoTracking()
            .Select(c => new JournalCourseRecord(c.Id, c.Name, c.Section))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Q2 — the items of the course with <c>start &lt;= ItemDate &lt; endExclusive</c>. The entities are read
    /// untracked so <c>CourseWork.Kind</c> stays the only definition of the kind (entity model §1).
    /// </summary>
    public async Task<IReadOnlyList<JournalItemRecord>> GetItemsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        RequireUtcPeriod(start, endExclusive);
        var items = await db.CourseWorks
            .AsNoTracking()
            .Where(w => w.CourseId == courseId && w.ItemDate >= start && w.ItemDate < endExclusive)
            .ToListAsync(cancellationToken);
        return items
            .Select(w => new JournalItemRecord(w.Id, w.Title, w.ItemDate, w.DueAt, w.MaxPoints, w.Kind))
            .ToList();
    }

    /// <summary>Q3 — every student membership of the course with its participant, leavers included.</summary>
    public async Task<IReadOnlyList<JournalMemberRecord>> GetStudentMembersAsync(long courseId, CancellationToken cancellationToken) =>
        await db.CourseMemberships
            .AsNoTracking()
            .Where(m => m.CourseId == courseId && m.Role == ClassroomRole.Student)
            .Select(m => new JournalMemberRecord(
                m.ParticipantId,
                m.FirstSeenAt,
                m.LastSeenAt,
                m.OnRoster,
                m.Participant.FullName,
                m.Participant.Email))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Q4 — every submission to an item of the course dated in the period. The period is repeated through the join
    /// so the parameter count is fixed (db-design §2).
    /// </summary>
    public async Task<IReadOnlyList<JournalSubmissionRecord>> GetSubmissionsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        RequireUtcPeriod(start, endExclusive);
        return await db.Submissions
            .AsNoTracking()
            .Join(
                db.CourseWorks.Where(w => w.CourseId == courseId && w.ItemDate >= start && w.ItemDate < endExclusive),
                s => s.CourseWorkId,
                w => w.Id,
                (s, _) => new JournalSubmissionRecord(
                    s.Id,
                    s.CourseWorkId,
                    s.ParticipantId,
                    s.UpdateTime,
                    s.State,
                    s.RawState,
                    s.AssignedGrade,
                    s.DraftGrade,
                    s.TurnedInAt,
                    s.Late))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Entity model §2: UTC bounds (Npgsql refuses another offset for <c>timestamptz</c>) and a non-empty period.</summary>
    private static void RequireUtcPeriod(DateTimeOffset start, DateTimeOffset endExclusive)
    {
        if (start.Offset != TimeSpan.Zero || endExclusive.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The period bounds must be UTC instants.", nameof(start));
        }

        if (start >= endExclusive)
        {
            throw new ArgumentException("The period must end after it starts.", nameof(endExclusive));
        }
    }
}
