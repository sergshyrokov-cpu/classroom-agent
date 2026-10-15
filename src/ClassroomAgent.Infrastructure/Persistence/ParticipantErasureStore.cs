using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// US-038 db-design §7.1: the erasure's deletes and clearings inside the caller's transaction. The rows are removed and
/// changed through the change tracker, not by set-based statements, so the commit still passes the read-only backstop
/// and set-based deletes stay confined to the retention purge (US-037 db-design §6). Opens no transaction and never
/// saves (AD-7); the use case's one commit writes everything, children before parents (PC-8).
/// </summary>
public sealed class ParticipantErasureStore(ClassroomAgentDbContext db) : IParticipantErasureStore
{
    public async Task<ErasureCounts?> EraseAsync(
        long participantId,
        string googleUserId,
        string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        var participant = await db.ClassroomParticipants.SingleOrDefaultAsync(p => p.Id == participantId, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var participations = 0;
        var organizers = 0;
        if (normalizedEmail is not null)
        {
            // Spec FR-006: matched case-insensitively (served by the two lower(…) expression indexes).
            var meetRows = await db.MeetParticipations
                .Where(p => p.Email != null && p.Email.ToLower() == normalizedEmail)
                .ToListAsync(cancellationToken);
            db.MeetParticipations.RemoveRange(meetRows);
            participations = meetRows.Count;

            var meetings = await db.MeetSessions
                .Where(s => s.OrganizerEmail != null && s.OrganizerEmail.ToLower() == normalizedEmail)
                .ToListAsync(cancellationToken);
            foreach (var meeting in meetings)
            {
                meeting.ClearOrganizer();
            }

            organizers = meetings.Count;
        }

        var submissions = await db.Submissions.Where(s => s.ParticipantId == participantId).ToListAsync(cancellationToken);
        db.Submissions.RemoveRange(submissions);

        var memberships = await db.CourseMemberships.Where(m => m.ParticipantId == participantId).ToListAsync(cancellationToken);
        db.CourseMemberships.RemoveRange(memberships);

        var courses = await db.Courses.Where(c => c.OwnerGoogleId == googleUserId).ToListAsync(cancellationToken);
        foreach (var course in courses)
        {
            course.ClearOwner();
        }

        db.ClassroomParticipants.Remove(participant);

        return new ErasureCounts(memberships.Count, submissions.Count, participations, organizers, courses.Count);
    }
}
