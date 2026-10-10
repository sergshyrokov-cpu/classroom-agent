using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// US-037 db-design §3 … §6: the purge's grouped read and set-based deletes over this scope's context. Every delete is
/// a single statement that returns its affected-row count; none loads an entity, and none opens a transaction — the
/// use case owns every boundary (AD-7). Children go before parents, because every foreign key is <c>Restrict</c>
/// (PC-8): nothing here relies on a cascade.
/// </summary>
/// <remarks>
/// These statements bypass <c>SaveChangesAsync</c> and therefore the read-only commit backstop (db-design §6). That is
/// acceptable only because this store is used by the one use case declared as the BR-026 retention purge, and the
/// architecture test keeps set-based deletes out of every other file.
/// </remarks>
public sealed class RetentionPurgeStore(ClassroomAgentDbContext db) : IRetentionPurgeStore
{
    public async Task<IReadOnlyList<CourseActivityDates>> GetCourseActivityDatesAsync(CancellationToken cancellationToken) =>
        await db.Courses
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CourseActivityDates(
                c.Id,
                c.UpdateTime,
                c.CreatedAt,
                db.CourseWorks.Where(w => w.CourseId == c.Id).Max(w => w.CreationTime),
                db.CourseWorks.Where(w => w.CourseId == c.Id).Max(w => w.UpdateTime),
                db.Submissions
                    .Where(s => db.CourseWorks.Any(w => w.Id == s.CourseWorkId && w.CourseId == c.Id))
                    .Max(s => s.UpdateTime),
                // US-032 db-design §6 step 1: the latest meeting reached through the course's linked codes.
                db.MeetSessions
                    .Where(m => db.MeetingCodeLinks.Any(l => l.CourseId == c.Id && l.MeetingCode == m.MeetingCode))
                    .Max(m => (DateTimeOffset?)m.StartedAt)))
            .ToListAsync(cancellationToken);

    public async Task<CourseDeletionCounts> DeleteCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        // US-032 db-design §6 step 2: the meetings reached through the course's links go first, child-first.
        var meetParticipations = await db.MeetParticipations
            .Where(p => db.MeetSessions.Any(s => s.Id == p.MeetSessionId
                                                 && db.MeetingCodeLinks.Any(l => l.CourseId == courseId
                                                                                 && l.MeetingCode == s.MeetingCode)))
            .ExecuteDeleteAsync(cancellationToken);
        var meetSessions = await db.MeetSessions
            .Where(s => db.MeetingCodeLinks.Any(l => l.CourseId == courseId && l.MeetingCode == s.MeetingCode))
            .ExecuteDeleteAsync(cancellationToken);
        var meetCodeLinks = await db.MeetingCodeLinks
            .Where(l => l.CourseId == courseId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.Submissions
            .Where(s => db.CourseWorks.Any(w => w.Id == s.CourseWorkId && w.CourseId == courseId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.CourseWorks.Where(w => w.CourseId == courseId).ExecuteDeleteAsync(cancellationToken);
        await db.CourseMemberships.Where(m => m.CourseId == courseId).ExecuteDeleteAsync(cancellationToken);
        await db.Courses.Where(c => c.Id == courseId).ExecuteDeleteAsync(cancellationToken);
        return new CourseDeletionCounts(meetSessions, meetParticipations, meetCodeLinks);
    }

    public async Task<IReadOnlyList<long>> GetCourseIdsWithExpiredLeaversAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken) =>
        await db.CourseMemberships
            .Where(m => !m.OnRoster && m.LastSeenAt < cutoff)
            .Select(m => m.CourseId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

    public async Task<LeaverDeletionCounts> DeleteExpiredLeaversAsync(
        long courseId,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        // US-032 db-design §6 step 3: before the memberships go, the leavers' participations (email matched
        // case-insensitively) in meetings whose code is linked to this course.
        var meetParticipations = await db.MeetParticipations
            .Where(p => p.Email != null
                        && db.MeetSessions.Any(s => s.Id == p.MeetSessionId
                                                    && db.MeetingCodeLinks.Any(l => l.CourseId == courseId
                                                                                    && l.MeetingCode == s.MeetingCode))
                        && db.CourseMemberships.Any(m => m.CourseId == courseId
                                                         && !m.OnRoster
                                                         && m.LastSeenAt < cutoff
                                                         && m.Participant.Email != null
                                                         && m.Participant.Email.ToLower() == p.Email.ToLower()))
            .ExecuteDeleteAsync(cancellationToken);
        await db.Submissions
            .Where(s => db.CourseWorks.Any(w => w.Id == s.CourseWorkId && w.CourseId == courseId)
                        && db.CourseMemberships.Any(m => m.CourseId == courseId
                                                         && m.ParticipantId == s.ParticipantId
                                                         && !m.OnRoster
                                                         && m.LastSeenAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken);
        var memberships = await db.CourseMemberships
            .Where(m => m.CourseId == courseId && !m.OnRoster && m.LastSeenAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        return new LeaverDeletionCounts(memberships, meetParticipations);
    }

    public Task<int> DeleteOrphanedParticipantsAsync(CancellationToken cancellationToken) =>
        db.ClassroomParticipants
            .Where(p => !db.CourseMemberships.Any(m => m.ParticipantId == p.Id))
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteExpiredAccountsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        db.AppUsers
            .Where(u => (u.LastSuccessfulSignInAt ?? u.CreatedAt) < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteAuditEventsOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        db.AuditEvents.Where(e => e.OccurredAt < cutoff).ExecuteDeleteAsync(cancellationToken);

    /// <summary>US-031 db-design §6 step 1: a batch of meetings that started before the cutoff, by id.</summary>
    public async Task<IReadOnlyList<long>> GetExpiredMeetSessionIdsAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken) =>
        await db.MeetSessions
            .AsNoTracking()
            .Where(s => s.StartedAt < cutoff)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// US-031 db-design §6 step 2: the participations of those meetings first (<c>RESTRICT</c>), then the meetings; the
    /// caller wraps both in one transaction, so a meeting is never left half-deleted (spec FR-013).
    /// </summary>
    public async Task<(int Sessions, int Participations)> DeleteMeetSessionsAsync(
        IReadOnlyCollection<long> sessionIds,
        CancellationToken cancellationToken)
    {
        var ids = sessionIds.ToList();
        var participations = await db.MeetParticipations
            .Where(p => ids.Contains(p.MeetSessionId))
            .ExecuteDeleteAsync(cancellationToken);
        var sessions = await db.MeetSessions
            .Where(s => ids.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);
        return (sessions, participations);
    }

    /// <summary>
    /// US-032 db-design §6 step 4: "not a course" marks whose code has no meeting left, in one set-based statement.
    /// Course links with no meeting left are deliberately not touched (spec FR-016 rule 4).
    /// </summary>
    public Task<int> DeleteOrphanedNotACourseMarksAsync(CancellationToken cancellationToken) =>
        db.MeetingCodeLinks
            .Where(l => l.CourseId == null && !db.MeetSessions.Any(s => s.MeetingCode == l.MeetingCode))
            .ExecuteDeleteAsync(cancellationToken);
}
