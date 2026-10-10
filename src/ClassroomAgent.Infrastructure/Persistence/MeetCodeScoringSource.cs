using ClassroomAgent.Application.MeetLinking;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// Bounded reads for scoring meeting codes (US-032 db-design §5.2): the unassigned codes in ordinal order, and for a
/// set of codes their meetings, participant emails and the memberships matching those emails case-insensitively
/// (<c>lower(email)</c>, served by <c>ix_classroom_participant_email_lower</c>). Read-only, untracked, nothing logged
/// (SC-10).
/// </summary>
public sealed class MeetCodeScoringSource(ClassroomAgentDbContext db) : IMeetCodeScoringSource
{
    public async Task<IReadOnlyList<string>> GetUnassignedCodesAsync(
        string? afterCode,
        int batchSize,
        CancellationToken cancellationToken) =>
        // Raw SQL only for the ordinal ("C" collation) comparison and order; the empty string precedes every code.
        await db.Database
            .SqlQuery<string>($"""
                SELECT s.meeting_code AS "Value"
                FROM meet_session s
                WHERE NOT EXISTS (SELECT 1 FROM meeting_code_link l WHERE l.meeting_code = s.meeting_code)
                  AND s.meeting_code COLLATE "C" > {afterCode ?? string.Empty} COLLATE "C"
                GROUP BY s.meeting_code
                ORDER BY s.meeting_code COLLATE "C"
                LIMIT {batchSize}
                """)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MeetCodeScoringInput>> GetScoringInputsAsync(
        IReadOnlyCollection<string> meetingCodes,
        CancellationToken cancellationToken)
    {
        var codes = meetingCodes.Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count == 0)
        {
            return [];
        }

        var sessions = await db.MeetSessions
            .AsNoTracking()
            .Where(s => codes.Contains(s.MeetingCode))
            .Select(s => new { s.Id, s.MeetingCode, s.OrganizerEmail, s.StartedAt })
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return [];
        }

        var participations = await db.MeetParticipations
            .AsNoTracking()
            .Where(p => p.Email != null && db.MeetSessions.Any(s => s.Id == p.MeetSessionId && codes.Contains(s.MeetingCode)))
            .Select(p => new { p.MeetSessionId, Email = p.Email! })
            .ToListAsync(cancellationToken);
        var participantEmailsBySession = participations
            .GroupBy(p => p.MeetSessionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.Email).ToList());

        var lowered = sessions.Select(s => s.OrganizerEmail)
            .Concat(participations.Select(p => p.Email))
            .Select(e => e.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var memberships = await db.CourseMemberships
            .AsNoTracking()
            .Where(m => m.Participant.Email != null && lowered.Contains(m.Participant.Email.ToLower()))
            .Select(m => new RosterMembership(
                m.CourseId,
                m.Role,
                m.Participant.Email!,
                m.FirstSeenAt,
                m.LastSeenAt,
                m.OnRoster))
            .ToListAsync(cancellationToken);

        var inputs = new List<MeetCodeScoringInput>();
        foreach (var group in sessions.GroupBy(s => s.MeetingCode, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var meetings = group
                .OrderBy(s => s.StartedAt)
                .ThenBy(s => s.Id)
                .Select(s => new MeetCodeMeeting(
                    s.Id,
                    s.OrganizerEmail,
                    s.StartedAt,
                    participantEmailsBySession.TryGetValue(s.Id, out var emails) ? emails : []))
                .ToList();
            var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var meeting in meetings)
            {
                relevant.Add(meeting.OrganizerEmail);
                relevant.UnionWith(meeting.ParticipantEmails);
            }

            inputs.Add(new MeetCodeScoringInput(
                group.Key,
                meetings,
                memberships.Where(m => relevant.Contains(m.Email)).ToList()));
        }

        return inputs;
    }
}
