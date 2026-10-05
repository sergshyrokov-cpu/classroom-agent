using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The journal fields of a report over PostgreSQL (US-027 db-design §5.1, F1 … F4). One command each, no change
/// tracking, no order. The lesson date is <c>ScheduledTime ?? CreationTime ?? ItemDate</c> (FR-004); the same
/// expression filters and is projected, in F2 and in F4.
/// </summary>
public sealed class JournalFieldSource(ClassroomAgentDbContext db) : IJournalFieldSource
{
    /// <summary>F1 — every stored course.</summary>
    public async Task<IReadOnlyList<JournalCourseRecord>> GetCoursesAsync(CancellationToken cancellationToken) =>
        await db.Courses
            .AsNoTracking()
            .Select(c => new JournalCourseRecord(c.Id, c.Name, c.Section))
            .ToListAsync(cancellationToken);

    /// <summary>F2 — the items of the course whose lesson date is in the period.</summary>
    public async Task<IReadOnlyList<JournalLessonRecord>> GetLessonsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        RequireUtcPeriod(start, endExclusive);
        return await db.CourseWorks
            .AsNoTracking()
            .Where(w => w.CourseId == courseId
                && (w.ScheduledTime ?? w.CreationTime ?? w.ItemDate) >= start
                && (w.ScheduledTime ?? w.CreationTime ?? w.ItemDate) < endExclusive)
            .Select(w => new JournalLessonRecord(
                w.Id,
                w.Resource,
                w.Title,
                w.MaxPoints,
                w.DueAt,
                (w.ScheduledTime ?? w.CreationTime ?? w.ItemDate)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>F3 — every membership of the course, both roles, with its participant.</summary>
    public async Task<IReadOnlyList<JournalCourseMemberRecord>> GetMembersAsync(
        long courseId,
        CancellationToken cancellationToken) =>
        await db.CourseMemberships
            .AsNoTracking()
            .Where(m => m.CourseId == courseId)
            .Select(m => new JournalCourseMemberRecord(
                m.ParticipantId,
                m.Role,
                m.FirstSeenAt,
                m.LastSeenAt,
                m.OnRoster,
                m.Participant.Surname,
                m.Participant.GivenName,
                m.Participant.Email))
            .ToListAsync(cancellationToken);

    /// <summary>F4 — every submission to an item F2 returns, selected through the same predicate.</summary>
    public async Task<IReadOnlyList<JournalSubmissionRecord>> GetLessonSubmissionsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        RequireUtcPeriod(start, endExclusive);
        return await db.Submissions
            .AsNoTracking()
            .Join(
                db.CourseWorks.Where(w => w.CourseId == courseId
                    && (w.ScheduledTime ?? w.CreationTime ?? w.ItemDate) >= start
                    && (w.ScheduledTime ?? w.CreationTime ?? w.ItemDate) < endExclusive),
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

    /// <summary>UTC bounds (Npgsql refuses another offset for <c>timestamptz</c>) and a non-empty period.</summary>
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
