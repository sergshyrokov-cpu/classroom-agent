using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// IMeetReportSource over the database (US-033 entity model §6, db-design §3): untracked reads, no transaction, no
/// write, nothing logged (SC-10). A "not a course" link has no course id, so it never matches R1 or R3 (BR-083).
/// </summary>
public sealed class MeetReportSource(ClassroomAgentDbContext db) : IMeetReportSource
{
    /// <summary>db-design §3: R5's <c>ANY(@emails)</c> is sent in batches of this size.</summary>
    private const int EmailBatchSize = 1000;

    public async Task<string?> GetCourseNameAsync(long courseId, CancellationToken cancellationToken) =>
        await db.Courses
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => c.Name)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MeetReportMeetingRecord>> GetMeetingsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        // R1: meetings whose code is linked to the course, with the start in [start, endExclusive).
        var sessions = await db.MeetSessions
            .AsNoTracking()
            .Where(s => db.MeetingCodeLinks.Any(l => l.MeetingCode == s.MeetingCode && l.CourseId == courseId))
            .Where(s => s.StartedAt >= start && s.StartedAt < endExclusive)
            .Select(s => new { s.Id, s.MeetingCode, s.OrganizerEmail, s.StartedAt, s.EndedAt })
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return [];
        }

        // R2: their connections.
        var ids = sessions.Select(s => s.Id).ToList();
        var connections = await ConnectionsAsync(ids, cancellationToken);
        return sessions
            .Select(s => new MeetReportMeetingRecord(
                s.Id,
                s.MeetingCode,
                s.OrganizerEmail,
                s.StartedAt,
                s.EndedAt,
                connections.TryGetValue(s.Id, out var own) ? own : []))
            .ToList();
    }

    public async Task<MeetReportMeetingRecord?> GetMeetingOfCourseAsync(
        long courseId,
        long meetingId,
        CancellationToken cancellationToken)
    {
        // R3: the meeting only when its code is linked to this course.
        var session = await db.MeetSessions
            .AsNoTracking()
            .Where(s => s.Id == meetingId)
            .Where(s => db.MeetingCodeLinks.Any(l => l.MeetingCode == s.MeetingCode && l.CourseId == courseId))
            .Select(s => new { s.Id, s.MeetingCode, s.OrganizerEmail, s.StartedAt, s.EndedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        var connections = await ConnectionsAsync([session.Id], cancellationToken);
        return new MeetReportMeetingRecord(
            session.Id,
            session.MeetingCode,
            session.OrganizerEmail,
            session.StartedAt,
            session.EndedAt,
            connections.TryGetValue(session.Id, out var own) ? own : []);
    }

    /// <summary>R4: every membership of the course, any role.</summary>
    public async Task<IReadOnlyList<MeetReportMemberRecord>> GetCourseMembersAsync(
        long courseId,
        CancellationToken cancellationToken) =>
        await db.CourseMemberships
            .AsNoTracking()
            .Where(m => m.CourseId == courseId)
            .Select(m => new MeetReportMemberRecord(
                m.ParticipantId,
                m.Role,
                m.Participant.Email,
                m.Participant.FullName,
                m.FirstSeenAt,
                m.LastSeenAt,
                m.OnRoster))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MeetReportPersonRecord>> GetPeopleAsync(
        IReadOnlyCollection<string> lowerCaseEmails,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lowerCaseEmails);
        var emails = lowerCaseEmails.Select(e => e.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList();
        var people = new List<MeetReportPersonRecord>();
        foreach (var batch in emails.Chunk(EmailBatchSize))
        {
            // R5: participants by lower(email), served by ix_classroom_participant_email_lower.
            var participants = await db.ClassroomParticipants
                .AsNoTracking()
                .Where(p => p.Email != null && batch.Contains(p.Email.ToLower()))
                .Select(p => new
                {
                    p.Id,
                    Email = p.Email!.ToLower(),
                    p.FullName,
                    HasMembership = db.CourseMemberships.Any(m => m.ParticipantId == p.Id),
                })
                .ToListAsync(cancellationToken);

            // db-design §4: personal when any has a membership; the name is the lowest id's that has one.
            people.AddRange(participants
                .GroupBy(p => p.Email, StringComparer.Ordinal)
                .Select(g => new MeetReportPersonRecord(
                    g.Key,
                    g.Where(p => p.FullName != null).OrderBy(p => p.Id).Select(p => p.FullName).FirstOrDefault(),
                    g.Any(p => p.HasMembership))));
        }

        return people;
    }

    private async Task<Dictionary<long, IReadOnlyList<MeetReportConnectionRecord>>> ConnectionsAsync(
        List<long> sessionIds,
        CancellationToken cancellationToken)
    {
        var rows = await db.MeetParticipations
            .AsNoTracking()
            .Where(p => sessionIds.Contains(p.MeetSessionId))
            .OrderBy(p => p.Id)
            .Select(p => new { p.MeetSessionId, p.Email, p.DurationSeconds })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.MeetSessionId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MeetReportConnectionRecord>)g
                    .Select(r => new MeetReportConnectionRecord(r.Email, r.DurationSeconds))
                    .ToList());
    }
}
