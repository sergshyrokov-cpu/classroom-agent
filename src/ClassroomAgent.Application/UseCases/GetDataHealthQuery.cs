using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-062 entity model §2.4, api-design §2.8: the «Стан даних» page, shaped for the caller's role. Spec §7: the Dean's
/// model carries no technical details at all — the restriction is the model's content, not hidden markup (AD-8).
/// </summary>
public sealed class GetDataHealthQuery(
    IDataHealthSource source,
    ISyncStateRepository syncStates,
    ISyncSkippedCourseRepository skippedCourses,
    ILegitimacyStateRepository legitimacyStates,
    TimeProvider timeProvider)
{
    /// <summary>FR-004.3: the journal of a stored course.</summary>
    private const string JournalPathPrefix = "/workspace/journal?courseId=";

    public async Task<DataHealthPageModel> ExecuteAsync(AppRole role, CancellationToken cancellationToken)
    {
        var isAdmin = role == AppRole.Admin;
        var assessment = await DataHealthRules.AssessAsync(
            source, syncStates, skippedCourses, legitimacyStates, timeProvider, cancellationToken);

        // Db-design §5.3: the detail queries run only for the courses whose fact says they have something to show.
        var gradeCourses = assessment.Courses
            .Where(c => c.HasGradeAboveMaximum && c.Course.CourseId is not null)
            .Select(c => c.Course.CourseId!.Value)
            .ToList();
        var itemCourses = assessment.Courses
            .Where(c => c.HasItemNotInClassroom && c.Course.CourseId is not null)
            .Select(c => c.Course.CourseId!.Value)
            .ToList();
        var grades = gradeCourses.Count == 0
            ? []
            : await source.GetAboveMaximumGradesAsync(gradeCourses, cancellationToken);
        var items = itemCourses.Count == 0
            ? []
            : await source.GetItemsNotInClassroomAsync(itemCourses, cancellationToken);
        var gradesOf = grades.ToLookup(g => g.CourseId);
        var itemsOf = items.ToLookup(i => i.CourseId);

        CourseProblems Problems(DataHealthRules.CourseEntry entry, bool? unknownState)
        {
            var id = entry.Course.CourseId;
            return new CourseProblems(
                entry.Course,
                id is { } courseId ? JournalPathPrefix + courseId.ToString(CultureInfo.InvariantCulture) : null,
                entry.NotInClassroomSince,
                entry.Skipped,
                id is { } withItems
                    ? itemsOf[withItems].Select(i => new ItemNotInClassroom(i.Title, i.Since)).ToList()
                    : [],
                id is { } withGrades
                    ? gradesOf[withGrades]
                        .Select(g => new AboveMaximumGrade(g.ItemTitle, g.StudentName, g.Points, g.Maximum, g.IsDraft))
                        .ToList()
                    : [],
                unknownState);
        }

        var teaching = assessment.Courses.Where(c => !c.Archived).Select(c => Problems(c, null)).ToList();
        var archived = new List<CourseProblems>();
        foreach (var entry in assessment.Courses.Where(c => c.Archived))
        {
            if (!entry.ArchivedUnknownState)
            {
                archived.Add(Problems(entry, null));
            }
            else if (isAdmin)
            {
                archived.Add(Problems(entry, true));
            }
            else
            {
                // OD-004 a, FR-002.3: the Dean gets the archived X3 as a title without the course; any teaching problem
                // of the same course stays a teaching entry with its name.
                archived.Add(new CourseProblems(new CourseLabel(null, null, null), null, null, null, [], [], true));
                if (entry.TeachingKinds > 0)
                {
                    archived.Add(Problems(entry, null));
                }
            }
        }

        var technical = isAdmin
            ? assessment.Technical
            : assessment.Technical.Select(t => t with { Details = null }).ToList();
        var readOnly = assessment.Mode is { IsReadOnly: true, Reason: { } reason } ? new ReadOnlyNotice(reason) : null;
        return new DataHealthPageModel(
            isAdmin,
            null,
            null,
            readOnly,
            technical.Count == 0 && teaching.Count == 0 && archived.Count == 0,
            technical,
            teaching,
            archived);
    }
}
