using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// IStudentSearchSource over the database (US-071 db-design §3): untracked reads, one command each, no transaction, no write,
/// nothing logged (SC-10). The match is <see cref="SearchMatch"/>'s and the order <see cref="ParticipantLabel"/>'s.
/// </summary>
public sealed class StudentSearchSource(ClassroomAgentDbContext db) : IStudentSearchSource
{
    private const string Collation = ClassroomAgentDbContext.CourseListCollation;

    public Task<int> CountStudentsAsync(string search, CancellationToken cancellationToken) =>
        Listed(search).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentSearchRecord>> GetStudentsAsync(string search, int limit, CancellationToken cancellationToken) =>
        await Listed(search)
            .Select(ParticipantLabel.Projection)
            .OrderByLabel()
            .Take(limit)
            .Select(x => new StudentSearchRecord(x.Id, x.Surname, x.GivenName, x.Email))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentSearchCourseRecord>> GetStudentCoursesAsync(
        IReadOnlyCollection<long> participantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participantIds);
        if (participantIds.Count == 0)
        {
            return [];
        }

        var ids = participantIds.ToArray();

        // Db-design §3: the course list's order; SeenOnce is the journal's expression (US-060 OD-003 a).
        return await db.CourseMemberships
            .AsNoTracking()
            .Where(m => ids.Contains(m.ParticipantId) && m.Role == ClassroomRole.Student)
            .OrderBy(m => EF.Functions.Collate(m.Course.Name, Collation))
            .ThenBy(m => m.Course.Section == null ? 0 : 1)
            .ThenBy(m => EF.Functions.Collate(m.Course.Section, Collation))
            .ThenBy(m => m.CourseId)
            .Select(m => new StudentSearchCourseRecord(
                m.ParticipantId,
                m.CourseId,
                m.Course.Name,
                m.Course.Section,
                m.OnRoster,
                m.FirstSeenAt == m.LastSeenAt,
                m.LastSeenAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Db-design §3: a person with a student membership in any course, on the roster or not, holding every word in surname,
    /// given name or email. With neither name nor email every field is null, so no word matches (spec I-4).
    /// </summary>
    private IQueryable<ClassroomParticipant> Listed(string search)
    {
        var listed = db.ClassroomParticipants
            .AsNoTracking()
            .Where(p => db.CourseMemberships.Any(m => m.ParticipantId == p.Id && m.Role == ClassroomRole.Student));
        foreach (var word in SearchMatch.Words(search))
        {
            listed = listed.Where(SearchMatch.AnyOf(
                SearchMatch.InField<ClassroomParticipant>(p => p.Surname, word),
                SearchMatch.InField<ClassroomParticipant>(p => p.GivenName, word),
                SearchMatch.InField<ClassroomParticipant>(p => p.Email, word)));
        }

        return listed;
    }
}
