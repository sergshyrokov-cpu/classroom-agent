using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// ICourseListSource over the database (US-020 db-design §3, entity model §2): untracked reads, one command each, no
/// transaction, no write, nothing logged (SC-10). Order and search run under the collation <c>course_name_uk</c>
/// (db-design §2); the search text is a bound parameter matched literally (S-09).
/// </summary>
public sealed class CourseListSource(ClassroomAgentDbContext db) : ICourseListSource
{
    private const string Collation = ClassroomAgentDbContext.CourseListCollation;

    public async Task<IReadOnlyList<CourseListPersonRecord>> GetTeacherChoiceAsync(CancellationToken cancellationToken) =>
        await db.ClassroomParticipants
            .AsNoTracking()
            .Where(p => db.CourseMemberships.Any(m =>
                m.ParticipantId == p.Id && m.Role == ClassroomRole.Teacher && m.OnRoster))
            .Select(p => new
            {
                ParticipantId = p.Id,
                p.Surname,
                p.GivenName,
                p.Email,
                Label = p.Surname != null && p.Surname != string.Empty && p.GivenName != null && p.GivenName != string.Empty
                    ? p.Surname + " " + p.GivenName
                    : p.Surname != null && p.Surname != string.Empty
                        ? p.Surname
                        : p.GivenName != null && p.GivenName != string.Empty
                            ? p.GivenName
                            : p.Email != null && p.Email != string.Empty
                                ? p.Email
                                : null,
            })
            .OrderBy(x => x.Label == null ? 1 : 0)
            .ThenBy(x => EF.Functions.Collate(x.Label, Collation))
            .ThenBy(x => x.ParticipantId)
            .Select(x => new CourseListPersonRecord(x.ParticipantId, x.Surname, x.GivenName, x.Email))
            .ToListAsync(cancellationToken);

    public Task<int> CountCoursesAsync(CourseListFilter filter, CancellationToken cancellationToken) =>
        Matching(filter).CountAsync(cancellationToken);

    public Task<bool> AnyCourseStoredAsync(CancellationToken cancellationToken) =>
        db.Courses.AsNoTracking().AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<CourseListCourseRecord>> GetCoursePageAsync(
        CourseListFilter filter, int offset, int limit, CancellationToken cancellationToken) =>
        await Matching(filter)
            .OrderBy(c => EF.Functions.Collate(c.Name, Collation))
            .ThenBy(c => c.Section == null ? 0 : 1)
            .ThenBy(c => EF.Functions.Collate(c.Section, Collation))
            .ThenBy(c => c.Id)
            .Skip(offset)
            .Take(limit)
            .Select(c => new CourseListCourseRecord(c.Id, c.Name, c.Section, c.State, c.UpdateTime))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CourseListTeacherRecord>> GetCurrentTeachersAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken)
    {
        var ids = courseIds.ToList();
        var rows = await db.CourseMemberships
            .AsNoTracking()
            .Where(m => ids.Contains(m.CourseId) && m.Role == ClassroomRole.Teacher && m.OnRoster)
            .Select(m => new
            {
                m.CourseId,
                ParticipantId = m.Participant.Id,
                m.Participant.Surname,
                m.Participant.GivenName,
                m.Participant.Email,
                Label = m.Participant.Surname != null && m.Participant.Surname != string.Empty
                && m.Participant.GivenName != null && m.Participant.GivenName != string.Empty
                    ? m.Participant.Surname + " " + m.Participant.GivenName
                    : m.Participant.Surname != null && m.Participant.Surname != string.Empty
                        ? m.Participant.Surname
                        : m.Participant.GivenName != null && m.Participant.GivenName != string.Empty
                            ? m.Participant.GivenName
                            : m.Participant.Email != null && m.Participant.Email != string.Empty
                                ? m.Participant.Email
                                : null,
            })
            .OrderBy(x => x.CourseId)
            .ThenBy(x => x.Label == null ? 1 : 0)
            .ThenBy(x => EF.Functions.Collate(x.Label, Collation))
            .ThenBy(x => x.ParticipantId)
            .ToListAsync(cancellationToken);
        return rows
            .Select(x => new CourseListTeacherRecord(
                x.CourseId, new CourseListPersonRecord(x.ParticipantId, x.Surname, x.GivenName, x.Email)))
            .ToList();
    }

    public async Task<IReadOnlyList<CourseListStudentCountRecord>> GetStudentCountsAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken)
    {
        var ids = courseIds.ToList();
        return await db.CourseMemberships
            .AsNoTracking()
            .Where(m => ids.Contains(m.CourseId) && m.Role == ClassroomRole.Student && m.OnRoster)
            .GroupBy(m => m.CourseId)
            .Select(g => new CourseListStudentCountRecord(g.Key, g.Count()))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Db-design §3.1, §3.2: state, current teacher and literal case-insensitive search, combined with AND.</summary>
    private IQueryable<Course> Matching(CourseListFilter filter)
    {
        var courses = db.Courses.AsNoTracking();
        if (filter.State is { } state)
        {
            courses = courses.Where(c => c.State == state);
        }

        if (filter.TeacherParticipantId is { } teacher)
        {
            courses = courses.Where(c => db.CourseMemberships.Any(m =>
                m.CourseId == c.Id && m.ParticipantId == teacher && m.Role == ClassroomRole.Teacher && m.OnRoster));
        }

        if (filter.SearchText is { } term)
        {
            courses = courses.Where(c =>
                EF.Functions.Collate(c.Name, Collation).ToLower()
                    .Contains(EF.Functions.Collate(term, Collation).ToLower())
                || (c.Section != null
                    && EF.Functions.Collate(c.Section, Collation).ToLower()
                        .Contains(EF.Functions.Collate(term, Collation).ToLower())));
        }

        return courses;
    }
}
