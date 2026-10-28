using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// IDataHealthSource over the database (US-062 db-design §5, entity model §2.1): untracked reads, one command each, no
/// transaction, no write, nothing logged (SC-10). The fact query decides presence only; counting the problems is a
/// business rule and stays in Application (db-design §5.1, AD-3).
/// </summary>
public sealed class DataHealthSource(ClassroomAgentDbContext db) : IDataHealthSource
{
    private const string Collation = ClassroomAgentDbContext.CourseListCollation;

    /// <summary>
    /// Db-design §5.1: one row per course with an absence mark, a graded submission above its item's maximum, an item
    /// "not in Classroom", or a skip record matching its Google id. <c>max_points &gt; 0</c> and the strict <c>&gt;</c>
    /// are <c>DataMarkRules.IsAboveMaximum</c>; a null grade or maximum compares as false.
    /// </summary>
    public async Task<IReadOnlyList<CourseHealthFact>> GetCourseFactsAsync(CancellationToken cancellationToken) =>
        await db.Courses
            .AsNoTracking()
            .Select(c => new
            {
                Course = c,
                AboveMaximum = db.CourseWorks.Any(w => w.CourseId == c.Id
                    && w.MaxPoints > 0
                    && db.Submissions.Any(s => s.CourseWorkId == w.Id
                        && (s.AssignedGrade > w.MaxPoints || s.DraftGrade > w.MaxPoints))),
                ItemGone = db.CourseWorks.Any(w => w.CourseId == c.Id && w.NotInClassroomSince != null),
                Skipped = db.SyncSkippedCourses.Any(k => k.CourseGoogleId == c.GoogleId),
            })
            .Where(x => x.Course.AbsenceMark != null || x.AboveMaximum || x.ItemGone || x.Skipped)
            .Select(x => new CourseHealthFact(
                x.Course.Id,
                x.Course.GoogleId,
                x.Course.Name,
                x.Course.State,
                x.Course.AbsenceMark,
                x.Course.AbsentSince,
                x.Course.LastCompleteReadAt,
                x.AboveMaximum,
                x.ItemGone))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Db-design §5.3: every grade above its item's maximum — the assigned and the draft grade of one submission are two
    /// rows, assigned first — ordered by item title, then the journal's student label (both under the course-name
    /// collation), then the participant. A participant with no label gets an empty name; the page words it.
    /// </summary>
    public async Task<IReadOnlyList<AboveMaximumGradeRow>> GetAboveMaximumGradesAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken)
    {
        var ids = courseIds.ToArray();
        var graded = await (
                from w in db.CourseWorks.AsNoTracking()
                join s in db.Submissions.AsNoTracking() on w.Id equals s.CourseWorkId
                join p in db.ClassroomParticipants.AsNoTracking() on s.ParticipantId equals p.Id
                where ids.Contains(w.CourseId)
                    && w.MaxPoints > 0
                    && (s.AssignedGrade > w.MaxPoints || s.DraftGrade > w.MaxPoints)
                select new
                {
                    w.CourseId,
                    w.Title,
                    Maximum = w.MaxPoints!.Value,
                    s.AssignedGrade,
                    s.DraftGrade,
                    ParticipantId = p.Id,
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
            .OrderBy(x => EF.Functions.Collate(x.Title, Collation))
            .ThenBy(x => x.Label == null ? 1 : 0)
            .ThenBy(x => EF.Functions.Collate(x.Label, Collation))
            .ThenBy(x => x.ParticipantId)
            .ToListAsync(cancellationToken);

        var rows = new List<AboveMaximumGradeRow>();
        foreach (var g in graded)
        {
            if (g.AssignedGrade is { } assigned && assigned > g.Maximum)
            {
                rows.Add(new AboveMaximumGradeRow(g.CourseId, g.Title, g.Label ?? string.Empty, assigned, g.Maximum, false));
            }

            if (g.DraftGrade is { } draft && draft > g.Maximum)
            {
                rows.Add(new AboveMaximumGradeRow(g.CourseId, g.Title, g.Label ?? string.Empty, draft, g.Maximum, true));
            }
        }

        return rows;
    }

    /// <summary>Db-design §5.3: every item marked "not in Classroom" in the given courses, by title.</summary>
    public async Task<IReadOnlyList<ItemNotInClassroomRow>> GetItemsNotInClassroomAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken)
    {
        var ids = courseIds.ToArray();
        return await db.CourseWorks
            .AsNoTracking()
            .Where(w => ids.Contains(w.CourseId) && w.NotInClassroomSince != null)
            .OrderBy(w => EF.Functions.Collate(w.Title, Collation))
            .ThenBy(w => w.Id)
            .Select(w => new ItemNotInClassroomRow(w.CourseId, w.Title, w.NotInClassroomSince!.Value))
            .ToListAsync(cancellationToken);
    }
}
