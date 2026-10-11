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

    /// <summary>Spec VR-004, I-5: the largest page number accepted.</summary>
    private const int MaxPage = 100_000;

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

        var filter = new CourseListFilter(StateOf(status), teacher, search);
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
                .Select(c => new CourseListRow(
                    c.CourseId,
                    c.Name,
                    c.Section,
                    c.State,
                    teachers.Where(t => t.CourseId == c.CourseId).Select(t => NameOf(t.Teacher)).ToList(),
                    counts.GetValueOrDefault(c.CourseId),
                    LocalTime(c.UpdateTime)))
                .ToList();
            list = new CourseListResult(null, total, shown, pageCount, PageSize, rows);
        }

        return new CourseListPageResult(
            CourseListPageOutcome.Shown,
            new CourseListPageModel(
                status, choice, teacher, search, [], list, CourseListUrl.Build(status, teacher, search, list.Page)));
    }

    /// <summary>Spec VR-001: absent is ACTIVE; one exact token, case-insensitive, untrimmed.</summary>
    private static CourseStatusFilter ReadStatus(IReadOnlyList<string?> values, List<CourseListMessageKey> messages)
    {
        switch (Single(values, out var text))
        {
            case Presence.Absent:
                return CourseStatusFilter.Active;
            case Presence.One:
                foreach (var candidate in Enum.GetValues<CourseStatusFilter>())
                {
                    if (string.Equals(text, CourseListUrl.Token(candidate), StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }

                break;
        }

        messages.Add(CourseListMessageKey.StatusMalformed);
        return CourseStatusFilter.Active;
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

    /// <summary>Spec VR-004: absent is 1; one integer of ASCII digits from 1 to 100 000.</summary>
    private static int ReadPage(IReadOnlyList<string?> values, List<CourseListMessageKey> messages)
    {
        switch (Single(values, out var text))
        {
            case Presence.Absent:
                return 1;
            case Presence.One
                when text.Length <= 6
                     && text.All(char.IsAsciiDigit)
                     && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var page)
                     && page is >= 1 and <= MaxPage:
                return page;
            default:
                messages.Add(CourseListMessageKey.PageMalformed);
                return 1;
        }
    }

    private static CourseState? StateOf(CourseStatusFilter status) => status switch
    {
        CourseStatusFilter.Active => CourseState.Active,
        CourseStatusFilter.Archived => CourseState.Archived,
        CourseStatusFilter.Provisioned => CourseState.Provisioned,
        CourseStatusFilter.Declined => CourseState.Declined,
        CourseStatusFilter.Suspended => CourseState.Suspended,
        _ => null,
    };

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
    private static CourseListPersonName NameOf(CourseListPersonRecord person)
    {
        var parts = new[] { person.Surname, person.GivenName }.Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (parts.Count > 0)
        {
            return new CourseListPersonName(CourseListPersonNameKind.ProfileName, string.Join(' ', parts));
        }

        return string.IsNullOrEmpty(person.Email)
            ? new CourseListPersonName(CourseListPersonNameKind.NoName, null)
            : new CourseListPersonName(CourseListPersonNameKind.Email, person.Email);
    }

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
