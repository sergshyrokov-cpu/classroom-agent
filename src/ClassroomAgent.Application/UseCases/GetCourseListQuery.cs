using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The course list (US-020 spec FR-001 … FR-018). Reads only: no read-only guard, no unit of work, no audit, no Google
/// port (spec FR-015).
/// </summary>
/// <remarks>
/// Order (api-design §3): the shape of all four parameters → the teacher choice (read on every answer, §2.5) → the
/// teacher in the choice → count, page, teachers and student counts of the page. No course is read for the list before
/// the query passes (spec FR-012, S-07). Ordering and paging happen in the port (entity model §2); this class keeps the
/// order it receives. The reads are a fixed number per request (spec FR-016).
/// </remarks>
public sealed class GetCourseListQuery(ICourseListSource source, SchoolTimeZone schoolTimeZone)
{
    /// <summary>Spec FR-008, OD-006.</summary>
    public const int PageSize = 50;

    /// <summary>Spec VR-003, I-4: characters of the trimmed search text.</summary>
    private const int MaxSearchLength = 100;

    public async Task<CourseListPageResult> ExecuteAsync(CourseListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = new List<CourseListMessageKey>();
        var status = ReadStatus(request.Status, messages);
        var teacher = ReadTeacher(request.TeacherId, messages);
        var search = ReadSearch(request.Q, messages);
        var page = ReadPage(request.Page, messages);

        // Api-design §2.5: the choice is needed on every answer, and the 404 check is membership in it.
        var people = await source.GetTeacherChoiceAsync(cancellationToken);
        var choice = Choice(people);

        if (messages.Count > 0)
        {
            return new CourseListPageResult(
                CourseListPageOutcome.Invalid,
                new CourseListPageModel(
                    status, choice, teacher, search, messages, null, CourseListUrl.Build(status, teacher, search, 1)));
        }

        if (teacher is { } chosen && people.All(p => p.ParticipantId != chosen))
        {
            return new CourseListPageResult(
                CourseListPageOutcome.TeacherUnknown,
                new CourseListPageModel(
                    status,
                    choice,
                    null,
                    search,
                    [CourseListMessageKey.TeacherUnknown],
                    null,
                    CourseListUrl.Build(status, null, search, 1)));
        }

        var filter = new CourseListFilter(CourseListQueryRules.StateOf(status), teacher, search);
        var total = await source.CountCoursesAsync(filter, cancellationToken);
        CourseListResult list;
        if (total == 0)
        {
            // Spec FR-011, I-5: page 1 with the empty state.
            var empty = await source.AnyCourseStoredAsync(cancellationToken)
                ? CourseListEmptyState.NoMatch
                : CourseListEmptyState.NoCoursesStored;
            list = new CourseListResult(empty, 0, 1, 1, PageSize, []);
        }
        else
        {
            // Spec FR-008, I-5: a page past the last shows the last.
            var pageCount = (total + PageSize - 1) / PageSize;
            var shown = Math.Min(page, pageCount);
            var courses = await source.GetCoursePageAsync(filter, (shown - 1) * PageSize, PageSize, cancellationToken);
            var ids = courses.Select(c => c.CourseId).ToList();
            var teachers = await source.GetCurrentTeachersAsync(ids, cancellationToken);
            var counts = (await source.GetStudentCountsAsync(ids, cancellationToken))
                .ToDictionary(c => c.CourseId, c => c.StudentCount);
            var rows = courses
                .Select(c =>
                {
                    var current = teachers.Where(t => t.CourseId == c.CourseId).Select(t => NameOf(t.Teacher)).ToList();

                    // US-060 spec FR-001, FR-002: the stored mark, and "no teacher in Classroom" when none is on the roster.
                    return new CourseListRow(
                        c.CourseId,
                        c.Name,
                        c.Section,
                        c.State,
                        current,
                        counts.GetValueOrDefault(c.CourseId),
                        LocalTime(c.UpdateTime),
                        DataMarkRules.Absence(c.AbsenceMark, c.AbsentSince, schoolTimeZone.Zone),
                        current.Count == 0);
                })
                .ToList();
            list = new CourseListResult(null, total, shown, pageCount, PageSize, rows);
        }

        return new CourseListPageResult(
            CourseListPageOutcome.Shown,
            new CourseListPageModel(
                status, choice, teacher, search, [], list, CourseListUrl.Build(status, teacher, search, list.Page)));
    }

    /// <summary>Spec VR-001 (shared with US-022, <see cref="CourseListQueryRules"/>).</summary>
    private static CourseStatusFilter ReadStatus(IReadOnlyList<string?> values, List<CourseListMessageKey> messages)
    {
        if (!CourseListQueryRules.TryReadStatus(values, out var status))
        {
            messages.Add(CourseListMessageKey.StatusMalformed);
        }

        return status;
    }

    /// <summary>Spec VR-002: absent is no filter; one positive 64-bit integer of ASCII digits.</summary>
    private static long? ReadTeacher(IReadOnlyList<string?> values, List<CourseListMessageKey> messages)
    {
        switch (Single(values, out var text))
        {
            case Presence.Absent:
                return null;
            case Presence.One when ParseCourseId(text) is { } id:
                return id;
            default:
                messages.Add(CourseListMessageKey.TeacherMalformed);
                return null;
        }
    }

    /// <summary>Spec VR-003, api-design §2.4: repeated → too long → control character; white space only is no search.</summary>
    private static string? ReadSearch(IReadOnlyList<string?> values, List<CourseListMessageKey> messages)
    {
        if (values.Count > 1)
        {
            messages.Add(CourseListMessageKey.SearchRepeated);
            return null;
        }

        var text = values.Count == 1 ? (values[0] ?? string.Empty).Trim() : string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        if (text.Length > MaxSearchLength)
        {
            messages.Add(CourseListMessageKey.SearchTooLong);
            return null;
        }

        if (text.Any(char.IsControl))
        {
            messages.Add(CourseListMessageKey.SearchControlCharacter);
            return null;
        }

        return text;
    }

    /// <summary>Spec VR-004 (shared with US-022, <see cref="CourseListQueryRules"/>).</summary>
    private static int ReadPage(IReadOnlyList<string?> values, List<CourseListMessageKey> messages)
    {
        if (!CourseListQueryRules.TryReadPage(values, out var page))
        {
            messages.Add(CourseListMessageKey.PageMalformed);
        }

        return page;
    }

    /// <summary>Spec FR-005, I-3: the choice in the port's order; same labels carry their emails.</summary>
    private static List<TeacherOption> Choice(IReadOnlyList<CourseListPersonRecord> people)
    {
        var names = people.Select(p => (Person: p, Name: NameOf(p))).ToList();
        var duplicated = names
            .Where(n => n.Name.Text is not null)
            .GroupBy(n => n.Name.Text, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        return names
            .Select(n => new TeacherOption(
                n.Person.ParticipantId,
                n.Name,
                n.Name.Text is { } text && duplicated.Contains(text) && !string.IsNullOrEmpty(n.Person.Email)
                    ? n.Person.Email
                    : null))
            .ToList();
    }

    /// <summary>Spec FR-007, entity model §4: profile name, else the full email, else no name — as stored.</summary>
    private static CourseListPersonName NameOf(CourseListPersonRecord person) =>
        CoursePersonNameRule.NameOf(person.Surname, person.GivenName, person.Email);

    /// <summary>Spec FR-003, api-design §2.8: the school's local time, to the minute; null stays null (I-8).</summary>
    private DateTime? LocalTime(DateTimeOffset? instant)
    {
        if (instant is not { } value)
        {
            return null;
        }

        var local = TimeZoneInfo.ConvertTime(value, schoolTimeZone.Zone).DateTime;
        return new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, DateTimeKind.Unspecified);
    }
}
