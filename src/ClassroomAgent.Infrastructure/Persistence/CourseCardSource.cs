using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// ICourseCardSource over the database (US-021 db-design §2, entity model §2): untracked reads, one command each, no
/// transaction, no write, nothing logged (SC-10). The Classroom link, teacher folder, calendar id and Google ids are not
/// selected (spec FR-002, S-10); the owner is compared in the database so no Google id leaves Infrastructure.
/// </summary>
public sealed class CourseCardSource(ClassroomAgentDbContext db) : ICourseCardSource
{
    private const string Collation = ClassroomAgentDbContext.CourseListCollation;

    public Task<CourseCardCourseRecord?> GetCourseAsync(long courseId, CancellationToken cancellationToken) =>
        db.Courses
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new CourseCardCourseRecord(
                c.Id,
                c.Name,
                c.Section,
                c.State,
                c.Room,
                c.DescriptionHeading,
                c.Description,
                c.CreationTime,
                c.UpdateTime,
                c.AbsenceMark,
                c.AbsentSince))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Db-design §2 Q2: named rows first, the US-020 label under <c>course_name_uk</c>, the email (a row without one
    /// first) under the same collation, then the participant id — a total, stable order.
    /// </summary>
    public async Task<IReadOnlyList<CourseCardMemberRecord>> GetMembersAsync(long courseId, CancellationToken cancellationToken) =>
        await db.CourseMemberships
            .AsNoTracking()
            .Where(m => m.CourseId == courseId)
            .Select(m => new
            {
                ParticipantId = m.Participant.Id,
                m.Participant.Surname,
                m.Participant.GivenName,
                m.Participant.Email,
                m.Role,
                m.OnRoster,
                m.LastSeenAt,
                IsOwner = m.Course.OwnerGoogleId != null && m.Participant.GoogleUserId == m.Course.OwnerGoogleId,
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
            .OrderBy(x => x.Label == null ? 1 : 0)
            .ThenBy(x => EF.Functions.Collate(x.Label, Collation))
            .ThenBy(x => x.Email == null || x.Email == string.Empty ? 0 : 1)
            .ThenBy(x => EF.Functions.Collate(x.Email, Collation))
            .ThenBy(x => x.ParticipantId)
            .Select(x => new CourseCardMemberRecord(
                x.ParticipantId, x.Surname, x.GivenName, x.Email, x.Role, x.OnRoster, x.LastSeenAt, x.IsOwner))
            .ToListAsync(cancellationToken);
}
