using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// ITeacherListSource over the database (US-022 db-design §3, entity model §2): untracked reads, one command each, no
/// transaction, no write, nothing logged (SC-10). The order is the course list's teacher choice order
/// (<see cref="ParticipantLabel"/>, db-design §3.3).
/// </summary>
public sealed class TeacherListSource(ClassroomAgentDbContext db) : ITeacherListSource
{
    public Task<int> CountTeachersAsync(CourseState? state, string? search, CancellationToken cancellationToken) =>
        Listed(state, search).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<TeacherListRecord>> GetTeacherPageAsync(
        CourseState? state, string? search, int offset, int limit, CancellationToken cancellationToken)
    {
        var matching = Matching(state);
        return await Listed(state, search)
            .Select(ParticipantLabel.Projection)
            .OrderByLabel()
            .Skip(offset)
            .Take(limit)
            .Select(x => new TeacherListRecord(
                x.Id, x.Surname, x.GivenName, x.Email, matching.Count(m => m.ParticipantId == x.Id)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Db-design §2: participants with at least one matching membership; US-070 db-design §3: holding every word.</summary>
    private IQueryable<ClassroomParticipant> Listed(CourseState? state, string? search)
    {
        var matching = Matching(state);
        var listed = db.ClassroomParticipants.AsNoTracking().Where(p => matching.Any(m => m.ParticipantId == p.Id));
        foreach (var word in SearchMatch.Words(search))
        {
            listed = listed.Where(SearchMatch.AnyOf(
                SearchMatch.InField<ClassroomParticipant>(p => p.Surname, word),
                SearchMatch.InField<ClassroomParticipant>(p => p.GivenName, word),
                SearchMatch.InField<ClassroomParticipant>(p => p.Email, word)));
        }

        return listed;
    }

    /// <summary>Db-design §2: current teacher memberships (the partial index's predicate) in a course of the state.</summary>
    private IQueryable<CourseMembership> Matching(CourseState? state)
    {
        var memberships = db.CourseMemberships.AsNoTracking()
            .Where(m => m.Role == ClassroomRole.Teacher && m.OnRoster);
        return state is { } chosen ? memberships.Where(m => m.Course.State == chosen) : memberships;
    }
}
