using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// US-038 db-design §10: the reads of the erasure pages and of the erasure itself. Untracked; a fixed number of round
/// trips; nothing logged. The search term is matched literally — EF Core escapes it before it reaches a pattern.
/// </summary>
public sealed class ParticipantErasureReadSource(ClassroomAgentDbContext db) : IParticipantErasureReadSource
{
    /// <summary>US-065 api-design §2.5: the search's cap; the lookup returns one more to tell whether there were more.</summary>
    private const int MaxEmailMatches = 50;

    public async Task<ParticipantSearchPage> SearchAsync(string term, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(term);
        var lowered = term.ToLowerInvariant();

        var rows = await db.ClassroomParticipants
            .AsNoTracking()
            .Where(p => (p.Email != null && p.Email.ToLower().Contains(lowered))
                        || (p.FullName != null && p.FullName.ToLower().Contains(lowered))
                        || (p.Surname != null && p.Surname.ToLower().Contains(lowered))
                        || (p.GivenName != null && p.GivenName.ToLower().Contains(lowered)))
            .OrderBy(p => p.FullName)
            .ThenBy(p => p.Email)
            .ThenBy(p => p.Id)
            .Take(limit)
            .Select(p => new ParticipantSearchRecord(
                p.Id,
                p.FullName,
                p.Email,
                db.CourseMemberships.Count(m => m.ParticipantId == p.Id && m.Role == ClassroomRole.Teacher),
                db.CourseMemberships.Count(m => m.ParticipantId == p.Id && m.Role == ClassroomRole.Student)))
            .ToListAsync(cancellationToken);
        return new ParticipantSearchPage(rows);
    }

    public async Task<ErasureConfirmationData?> GetConfirmationAsync(long participantId, CancellationToken cancellationToken)
    {
        var participant = await db.ClassroomParticipants
            .AsNoTracking()
            .Where(p => p.Id == participantId)
            .Select(p => new { p.Id, p.GoogleUserId, p.Email, p.FullName })
            .SingleOrDefaultAsync(cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var courses = await db.CourseMemberships
            .AsNoTracking()
            .Where(m => m.ParticipantId == participantId)
            .OrderBy(m => m.Course.Name)
            .ThenBy(m => m.CourseId)
            .Select(m => new ErasureCourseRecord(m.Course.Name, m.Role))
            .ToListAsync(cancellationToken);

        var submissions = await db.Submissions.CountAsync(s => s.ParticipantId == participantId, cancellationToken);
        var grades = await db.Submissions.CountAsync(
            s => s.ParticipantId == participantId && (s.AssignedGrade != null || s.DraftGrade != null),
            cancellationToken);

        var normalized = string.IsNullOrWhiteSpace(participant.Email) ? null : WorkspaceConnection.NormalizeEmail(participant.Email);
        var participations = normalized is null
            ? 0
            : await db.MeetParticipations.CountAsync(p => p.Email != null && p.Email.ToLower() == normalized, cancellationToken);
        var organized = normalized is null
            ? 0
            : await db.MeetSessions.CountAsync(s => s.OrganizerEmail != null && s.OrganizerEmail.ToLower() == normalized, cancellationToken);
        var owned = await db.Courses.CountAsync(c => c.OwnerGoogleId == participant.GoogleUserId, cancellationToken);

        return new ErasureConfirmationData(
            participant.Id,
            participant.FullName,
            participant.Email,
            courses,
            submissions,
            grades,
            participations,
            organized,
            owned);
    }

    public Task<ParticipantErasureTarget?> GetTargetAsync(long participantId, CancellationToken cancellationToken) =>
        db.ClassroomParticipants
            .AsNoTracking()
            .Where(p => p.Id == participantId)
            .Select(p => new ParticipantErasureTarget(p.Id, p.GoogleUserId, p.Email))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// US-065 db-design §3 step 2: <c>lower(email) = @normalized</c>, served by <c>ix_classroom_participant_email_lower</c>;
    /// every participant, on a roster or not, in the order and shape of <see cref="SearchAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<ParticipantSearchResult>> FindParticipantsByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(normalizedEmail);

        return await db.ClassroomParticipants
            .AsNoTracking()
            .Where(p => p.Email != null && p.Email.ToLower() == normalizedEmail)
            .OrderBy(p => p.FullName)
            .ThenBy(p => p.Email)
            .ThenBy(p => p.Id)
            .Take(MaxEmailMatches + 1)
            .Select(p => new ParticipantSearchResult(
                p.Id,
                p.FullName,
                p.Email,
                db.CourseMemberships.Count(m => m.ParticipantId == p.Id && m.Role == ClassroomRole.Teacher),
                db.CourseMemberships.Count(m => m.ParticipantId == p.Id && m.Role == ClassroomRole.Student)))
            .ToListAsync(cancellationToken);
    }
}
