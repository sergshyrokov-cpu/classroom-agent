using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The results page of the header search (US-071 spec FR-002 … FR-008, api-design D-2). Reads only: no read-only guard, no
/// unit of work, no audit, no Google port (FR-014).
/// </summary>
/// <remarks>
/// Order (openapi <c>getSearchResults</c>): the shape of <c>q</c> → the length → each group's total, then its rows only when
/// the total is above 0. The number of reads is fixed, whatever the number of words or students (spec section 9, db-design §5).
/// Sequential on purpose: all ports share one scoped database context.
/// </remarks>
public sealed class GetSearchResultsQuery(
    IStudentSearchSource students,
    ITeacherListSource teachers,
    ICourseListSource courses,
    IMeetCodesReadSource meetCodes,
    SchoolTimeZone schoolTimeZone)
{
    /// <summary>Spec FR-007, api-design D-12.</summary>
    public const int GroupSize = 20;

    /// <summary>Spec VR-003: in trimmed <see cref="string"/> length.</summary>
    public const int MinimumLength = 2;

    public async Task<SearchResultsPageModel> ExecuteAsync(SearchResultsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // VR-001, VR-002: the one implementation; a refused text is kept nowhere.
        if (CourseListQueryRules.ReadSearch(request.Q, out var search) is { } refusal)
        {
            return Nothing(SearchResultsState.Refused, null, refusal);
        }

        // FR-008, VR-003: not malformed, so 200, and a 1-character text stays in the field.
        if (search is null || search.Length < MinimumLength)
        {
            return Nothing(SearchResultsState.Prompt, search, null);
        }

        return new SearchResultsPageModel(
            SearchResultsState.Results,
            search,
            null,
            SearchResultsUrl.Build(search),
            await ReadStudentsAsync(search, cancellationToken),
            await ReadTeachersAsync(search, cancellationToken),
            await ReadCoursesAsync(search, cancellationToken),
            await ReadMeetCodesAsync(search, cancellationToken));
    }

    private static SearchResultsPageModel Nothing(SearchResultsState state, string? searchText, SearchRefusalKey? refusal) =>
        new(
            state,
            searchText,
            refusal,
            SearchResultsUrl.Build(searchText),
            new SearchResultsGroup<StudentSearchRow>([], 0),
            new SearchResultsGroup<TeacherSearchRow>([], 0),
            new SearchResultsGroup<CourseSearchRow>([], 0),
            new MeetCodeSearchGroup([], 0, null));

    /// <summary>FR-003: the shown students with all their courses in the port's order; the courses take one read.</summary>
    private async Task<SearchResultsGroup<StudentSearchRow>> ReadStudentsAsync(string search, CancellationToken cancellationToken)
    {
        var total = await students.CountStudentsAsync(search, cancellationToken);
        if (total == 0)
        {
            return new SearchResultsGroup<StudentSearchRow>([], 0);
        }

        var records = await students.GetStudentsAsync(search, GroupSize, cancellationToken);
        if (records.Count == 0)
        {
            return new SearchResultsGroup<StudentSearchRow>([], total);
        }

        var ids = records.Select(r => r.ParticipantId).ToList();
        var memberships = await students.GetStudentCoursesAsync(ids, cancellationToken);
        var byStudent = memberships.ToLookup(m => m.ParticipantId);
        var zone = schoolTimeZone.Zone;
        var rows = records
            .Select(r => new StudentSearchRow(
                r.ParticipantId,
                LabelOf(r.Surname, r.GivenName, r.Email),
                EmailOf(r.Email),
                byStudent[r.ParticipantId]
                    .Select(m => new StudentSearchCourse(
                        m.CourseId,
                        m.Name,
                        m.Section,
                        DataMarkRules.Left(m.OnRoster, m.SeenOnce, m.LastSeenAt, zone)))
                    .ToList()))
            .ToList();
        return new SearchResultsGroup<StudentSearchRow>(rows, total);
    }

    /// <summary>FR-004, I-6: the teacher list at status "all", so the total equals the list's by construction (FR-007).</summary>
    private async Task<SearchResultsGroup<TeacherSearchRow>> ReadTeachersAsync(string search, CancellationToken cancellationToken)
    {
        var total = await teachers.CountTeachersAsync(null, search, cancellationToken);
        if (total == 0)
        {
            return new SearchResultsGroup<TeacherSearchRow>([], 0);
        }

        var records = await teachers.GetTeacherPageAsync(null, search, 0, GroupSize, cancellationToken);
        var rows = records
            .Select(r => new TeacherSearchRow(r.ParticipantId, LabelOf(r.Surname, r.GivenName, r.Email), EmailOf(r.Email)))
            .ToList();
        return new SearchResultsGroup<TeacherSearchRow>(rows, total);
    }

    /// <summary>FR-005, I-6: the course list at status "all", in its order.</summary>
    private async Task<SearchResultsGroup<CourseSearchRow>> ReadCoursesAsync(string search, CancellationToken cancellationToken)
    {
        var filter = new CourseListFilter(null, null, search);
        var total = await courses.CountCoursesAsync(filter, cancellationToken);
        if (total == 0)
        {
            return new SearchResultsGroup<CourseSearchRow>([], 0);
        }

        var records = await courses.GetCoursePageAsync(filter, 0, GroupSize, cancellationToken);
        var rows = records.Select(r => new CourseSearchRow(r.CourseId, r.Name, r.Section, r.State)).ToList();
        return new SearchResultsGroup<CourseSearchRow>(rows, total);
    }

    /// <summary>FR-006, FR-007: the total is the sum of the three counters; the whole-list link opens the first with a match (I-11).</summary>
    private async Task<MeetCodeSearchGroup> ReadMeetCodesAsync(string search, CancellationToken cancellationToken)
    {
        var counts = await meetCodes.GetCountsAsync(search, cancellationToken);
        var total = counts.Unassigned + counts.Linked + counts.NotACourse;
        if (total == 0)
        {
            return new MeetCodeSearchGroup([], 0, null);
        }

        var matches = await meetCodes.GetFirstMatchesAsync(search, GroupSize, cancellationToken);
        return new MeetCodeSearchGroup(matches, total, FirstListWithMatch(counts));
    }

    private static MeetCodeList FirstListWithMatch(MeetCodeListCounts counts)
    {
        if (counts.Unassigned > 0)
        {
            return MeetCodeList.Unassigned;
        }

        return counts.Linked > 0 ? MeetCodeList.Linked : MeetCodeList.NotACourse;
    }

    /// <summary>Entity model §3: a matched person has a name or an email, so the fallback only satisfies the nullable check.</summary>
    private static string LabelOf(string? surname, string? givenName, string? email) =>
        CoursePersonNameRule.NameOf(surname, givenName, email).Text ?? string.Empty;

    private static string? EmailOf(string? email) => string.IsNullOrEmpty(email) ? null : email;
}
