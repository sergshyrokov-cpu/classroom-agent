using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-020 AC-001 … AC-004, AC-006 … AC-008, AC-011, AC-014, AC-018, AC-020 (spec FR-002 … FR-008, FR-011, FR-015,
/// FR-016; entity model §3): the filter handed to the port, the row built from the port's records, the school's time
/// zone, paging with the page past the end, the two empty states, and a fixed number of reads whatever the data size.
/// The port is the in-memory <see cref="FakeCourseListSource"/>; ordering inside the database is proved against
/// PostgreSQL in <c>CourseListSourceTests</c>.
/// </summary>
public sealed class CourseListQueryTests
{
    public static TheoryData<string, CourseState> StateTokens => new()
    {
        { "active", CourseState.Active },
        { "archived", CourseState.Archived },
        { "provisioned", CourseState.Provisioned },
        { "declined", CourseState.Declined },
        { "suspended", CourseState.Suspended },
    };

    private static Task<CourseListPageResult> RunAsync(
        FakeCourseListSource source,
        string? status = null,
        string? teacherId = null,
        string? q = null,
        string? page = null) =>
        new GetCourseListQuery(source, new SchoolTimeZone(CourseListTestData.Kyiv)).ExecuteAsync(
            new CourseListRequest(One(status), One(teacherId), One(q), One(page)),
            TestContext.Current.CancellationToken);

    private static string?[] One(string? value) => value is null ? [] : [value];

    /// <summary>Adds <paramref name="count"/> active courses named "Курс 001", "Курс 002", ….</summary>
    private static List<long> AddNumberedCourses(FakeCourseListSource source, int count, CourseState state = CourseState.Active) =>
        Enumerable.Range(1, count).Select(i => source.AddCourse($"Курс {i:D3}", state: state)).ToList();

    /// <summary>AC-001, FR-003: the row carries the name, the section, the state, the current teachers and students, the update time.</summary>
    [Fact]
    public async Task TheRow_CarriesEveryColumnOfTheCourse()
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse(
            "Фізика", "9-Б", CourseState.Active, new DateTimeOffset(2026, 9, 1, 9, 15, 0, TimeSpan.Zero));
        var owner = source.AddPerson("Шевченко", "Тарас", "t.shevchenko@school.example");
        var coTeacher = source.AddPerson("Українка", "Леся", "l.ukrainka@school.example");
        source.AddTeacher(course, owner);
        source.AddTeacher(course, coTeacher);
        source.AddStudents(course, 3);

        var result = await RunAsync(source);

        var list = Assert.IsType<CourseListResult>(result.Page.List);
        var row = Assert.Single(list.Rows);
        Assert.Equal(course, row.CourseId);
        Assert.Equal("Фізика", row.Name);
        Assert.Equal("9-Б", row.Section);
        Assert.Equal(CourseState.Active, row.State);
        Assert.Equal(
            [
                new CourseListPersonName(CourseListPersonNameKind.ProfileName, "Українка Леся"),
                new CourseListPersonName(CourseListPersonNameKind.ProfileName, "Шевченко Тарас"),
            ],
            row.Teachers);
        Assert.Equal(3, row.StudentCount);
        Assert.Equal(new DateTime(2026, 9, 1, 12, 15, 0), row.UpdatedAt);
    }

    /// <summary>FR-003: no teacher — an empty list; no student — 0; no update time — null (I-8); no section — null.</summary>
    [Fact]
    public async Task ACourseWithoutTeachersStudentsSectionOrUpdateTime_HasEmptyValues()
    {
        var source = new FakeCourseListSource();
        source.AddCourse("Порожній курс");

        var result = await RunAsync(source);

        var row = Assert.Single(Assert.IsType<CourseListResult>(result.Page.List).Rows);
        Assert.Empty(row.Teachers);
        Assert.Equal(0, row.StudentCount);
        Assert.Null(row.UpdatedAt);
        Assert.Null(row.Section);
    }

    /// <summary>
    /// AC-018 (TC-8, NFR-074): 22:30 UTC on 31 March 2026 is 01:30 on 1 April in Kyiv (summer time began on 29 March);
    /// 21:30 UTC on 31 October is 23:30 the same day (winter time since 25 October).
    /// </summary>
    [Fact]
    public async Task TheUpdateTime_IsShownInTheSchoolsTimeZone_OnBothSidesOfSummerTime()
    {
        var source = new FakeCourseListSource();
        source.AddCourse("Алгебра", updateTime: new DateTimeOffset(2026, 3, 31, 22, 30, 0, TimeSpan.Zero));
        source.AddCourse("Біологія", updateTime: new DateTimeOffset(2026, 10, 31, 21, 30, 0, TimeSpan.Zero));

        var result = await RunAsync(source);

        var rows = Assert.IsType<CourseListResult>(result.Page.List).Rows;
        Assert.Equal(new DateTime(2026, 4, 1, 1, 30, 0), rows.Single(r => r.Name == "Алгебра").UpdatedAt);
        Assert.Equal(new DateTime(2026, 10, 31, 23, 30, 0), rows.Single(r => r.Name == "Біологія").UpdatedAt);
    }

    /// <summary>AC-001, FR-003: the students cell counts the current students only — a teacher or a leaver does not count.</summary>
    [Fact]
    public async Task TheStudentCount_IsTheCurrentStudentsOnly()
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse("Хімія");
        source.AddStudents(course, 2);
        source.AddStudent(course, source.AddPerson(email: "left@school.example"), onRoster: false);
        source.AddTeacher(course, source.AddPerson("Коваль", "Олена"));

        var result = await RunAsync(source);

        Assert.Equal(2, Assert.Single(Assert.IsType<CourseListResult>(result.Page.List).Rows).StudentCount);
    }

    /// <summary>AC-002, FR-004: no status — the filter is ACTIVE.</summary>
    [Fact]
    public async Task NoStatus_FiltersActive()
    {
        var source = new FakeCourseListSource();
        source.AddCourse("Активний");
        source.AddCourse("Архівний", state: CourseState.Archived);

        var result = await RunAsync(source);

        Assert.Equal(CourseState.Active, Assert.Single(source.CountFilters).State);
        Assert.Equal(["Активний"], Assert.IsType<CourseListResult>(result.Page.List).Rows.Select(r => r.Name));
    }

    /// <summary>AC-003, FR-004: each state token filters that state.</summary>
    [Theory]
    [MemberData(nameof(StateTokens))]
    public async Task AStateToken_FiltersThatState(string token, CourseState state)
    {
        var source = new FakeCourseListSource();
        foreach (var each in Enum.GetValues<CourseState>())
        {
            source.AddCourse("Курс " + each, state: each);
        }

        var result = await RunAsync(source, status: token);

        Assert.Equal(state, Assert.Single(source.CountFilters).State);
        Assert.Equal(state, Assert.Single(source.PageRequests).Filter.State);
        Assert.Equal(state, Assert.Single(Assert.IsType<CourseListResult>(result.Page.List).Rows).State);
    }

    /// <summary>AC-003, FR-004: "all" filters nothing — the port gets no state.</summary>
    [Fact]
    public async Task All_FiltersNoState()
    {
        var source = new FakeCourseListSource();
        foreach (var each in Enum.GetValues<CourseState>())
        {
            source.AddCourse("Курс " + each, state: each);
        }

        var result = await RunAsync(source, status: "all");

        Assert.Null(Assert.Single(source.CountFilters).State);
        Assert.Equal(5, Assert.IsType<CourseListResult>(result.Page.List).TotalCount);
    }

    /// <summary>AC-004, FR-005: the chosen teacher reaches the port as the participant id and is the effective value.</summary>
    [Fact]
    public async Task AChosenTeacher_ReachesThePort()
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse("Фізика");
        var teacher = source.AddPerson("Шевченко", "Тарас");
        source.AddTeacher(course, teacher);
        source.AddCourse("Хімія");

        var result = await RunAsync(source, teacherId: CourseListTestData.Id(teacher));

        Assert.Equal(CourseListPageOutcome.Shown, result.Outcome);
        Assert.Equal(teacher, result.Page.SelectedTeacherId);
        Assert.Equal(new CourseListFilter(CourseState.Active, teacher, null), Assert.Single(source.CountFilters));
        Assert.Equal([course], Assert.IsType<CourseListResult>(result.Page.List).Rows.Select(r => r.CourseId));
    }

    /// <summary>AC-004: the owner and the co-teacher are both in the choice, and choosing either lists the course.</summary>
    [Fact]
    public async Task OwnerAndCoTeacher_AreBothOffered_AndBothFindTheCourse()
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse("Фізика");
        var owner = source.AddPerson("Шевченко", "Тарас");
        var coTeacher = source.AddPerson("Українка", "Леся");
        var former = source.AddPerson("Франко", "Іван");
        source.AddTeacher(course, owner);
        source.AddTeacher(course, coTeacher);
        source.AddTeacher(course, former, onRoster: false);

        var byOwner = await RunAsync(source, teacherId: CourseListTestData.Id(owner));
        var byCoTeacher = await RunAsync(source, teacherId: CourseListTestData.Id(coTeacher));

        Assert.Equal([coTeacher, owner], byOwner.Page.TeacherChoice.Select(t => t.ParticipantId));
        Assert.Equal([course], Assert.IsType<CourseListResult>(byOwner.Page.List).Rows.Select(r => r.CourseId));
        Assert.Equal([course], Assert.IsType<CourseListResult>(byCoTeacher.Page.List).Rows.Select(r => r.CourseId));
    }

    /// <summary>AC-006, FR-006: the trimmed text reaches the port as the search and is the effective value.</summary>
    [Fact]
    public async Task TheSearchText_ReachesThePortTrimmed()
    {
        var source = new FakeCourseListSource();
        source.AddCourse("Історія України");
        source.AddCourse("Фізика", "історичний клас");
        source.AddCourse("Хімія");

        var result = await RunAsync(source, q: "  ІСТ ");

        Assert.Equal("ІСТ", result.Page.SearchText);
        Assert.Equal(new CourseListFilter(CourseState.Active, null, "ІСТ"), Assert.Single(source.CountFilters));
        Assert.Equal(2, Assert.IsType<CourseListResult>(result.Page.List).TotalCount);
    }

    /// <summary>AC-007, FR-002: status, teacher and search reach the port together.</summary>
    [Fact]
    public async Task AllThreeFilters_ReachThePortTogether()
    {
        var source = new FakeCourseListSource();
        var teacher = source.AddPerson("Шевченко", "Тарас");
        var match = source.AddCourse("Фізика 9", state: CourseState.Archived);
        var wrongState = source.AddCourse("Фізика 10");
        var wrongTeacher = source.AddCourse("Фізика 11", state: CourseState.Archived);
        var wrongName = source.AddCourse("Хімія", state: CourseState.Archived);
        source.AddTeacher(match, teacher);
        source.AddTeacher(wrongState, teacher);
        source.AddTeacher(wrongName, teacher);
        _ = wrongTeacher;

        var result = await RunAsync(source, "archived", CourseListTestData.Id(teacher), "фіз");

        Assert.Equal(new CourseListFilter(CourseState.Archived, teacher, "фіз"), Assert.Single(source.CountFilters));
        Assert.Equal([match], Assert.IsType<CourseListResult>(result.Page.List).Rows.Select(r => r.CourseId));
    }

    /// <summary>AC-008, FR-008: rows keep the port's order — ordering lives in the database (entity model §2).</summary>
    [Fact]
    public async Task TheRows_KeepThePortsOrder()
    {
        var source = new FakeCourseListSource();
        foreach (var name in new[] { "Історія", "Інформатика", "Географія", "Ґрунтознавство", "Алгебра" })
        {
            source.AddCourse(name);
        }

        var result = await RunAsync(source);

        Assert.Equal(
            ["Алгебра", "Географія", "Ґрунтознавство", "Інформатика", "Історія"],
            Assert.IsType<CourseListResult>(result.Page.List).Rows.Select(r => r.Name));
    }

    /// <summary>AC-008, AC-020, FR-008: 120 courses — 50 per page, page 2 reads offset 50, the totals are right.</summary>
    [Fact]
    public async Task Page2_Of120Courses_IsCourses51To100()
    {
        var source = new FakeCourseListSource();
        var ids = AddNumberedCourses(source, 120);

        var result = await RunAsync(source, page: "2");

        var list = Assert.IsType<CourseListResult>(result.Page.List);
        Assert.Equal(120, list.TotalCount);
        Assert.Equal(2, list.Page);
        Assert.Equal(3, list.PageCount);
        Assert.Equal(50, list.PageSize);
        Assert.Equal(ids.Skip(50).Take(50), list.Rows.Select(r => r.CourseId));
        var request = Assert.Single(source.PageRequests);
        Assert.Equal(50, request.Offset);
        Assert.Equal(50, request.Limit);
    }

    /// <summary>AC-020: page 3 of 120 is courses 101–120.</summary>
    [Fact]
    public async Task Page3_Of120Courses_IsCourses101To120()
    {
        var source = new FakeCourseListSource();
        var ids = AddNumberedCourses(source, 120);

        var result = await RunAsync(source, page: "3");

        var list = Assert.IsType<CourseListResult>(result.Page.List);
        Assert.Equal(3, list.Page);
        Assert.Equal(ids.Skip(100), list.Rows.Select(r => r.CourseId));
    }

    /// <summary>FR-008, I-5, api-design §2.9: a page past the last shows the last page, and the return path carries it.</summary>
    [Fact]
    public async Task APagePastTheLast_ShowsTheLastPage()
    {
        var source = new FakeCourseListSource();
        var ids = AddNumberedCourses(source, 120);

        var result = await RunAsync(source, status: "archived", page: "9");
        var active = await RunAsync(source, page: "9");

        var list = Assert.IsType<CourseListResult>(active.Page.List);
        Assert.Equal(CourseListPageOutcome.Shown, active.Outcome);
        Assert.Equal(3, list.Page);
        Assert.Equal(ids.Skip(100), list.Rows.Select(r => r.CourseId));
        Assert.Equal(100, source.PageRequests.Last().Offset);
        Assert.Equal(CourseListTestData.Path + "?page=3", active.Page.ReturnPath);
        Assert.Equal(1, Assert.IsType<CourseListResult>(result.Page.List).Page);
    }

    /// <summary>AC-008, FR-008: exactly 50 courses are one page; 51 are two.</summary>
    [Theory]
    [InlineData(50, 1)]
    [InlineData(51, 2)]
    [InlineData(1, 1)]
    public async Task ThePageCount_IsTheCeilingOfTotalOver50(int courses, int pages)
    {
        var source = new FakeCourseListSource();
        AddNumberedCourses(source, courses);

        var result = await RunAsync(source);

        Assert.Equal(pages, Assert.IsType<CourseListResult>(result.Page.List).PageCount);
    }

    /// <summary>AC-011, FR-011: no course stored — NoCoursesStored, page 1 of 1, no rows, no page read.</summary>
    [Fact]
    public async Task NoCourseStored_IsTheNoCoursesStoredState()
    {
        var source = new FakeCourseListSource();

        var result = await RunAsync(source, page: "4");

        var list = Assert.IsType<CourseListResult>(result.Page.List);
        Assert.Equal(CourseListPageOutcome.Shown, result.Outcome);
        Assert.Equal(CourseListEmptyState.NoCoursesStored, list.EmptyState);
        Assert.Equal(0, list.TotalCount);
        Assert.Equal(1, list.Page);
        Assert.Equal(1, list.PageCount);
        Assert.Empty(list.Rows);
        Assert.Empty(source.PageRequests);
        Assert.Equal(1, source.AnyCourseStoredCalls);
        Assert.Empty(result.Page.TeacherChoice);
    }

    /// <summary>AC-011, FR-011: courses stored but none matches — including the default ACTIVE filter over archived courses — is NoMatch.</summary>
    [Fact]
    public async Task NothingMatching_IsTheNoMatchState()
    {
        var source = new FakeCourseListSource();
        AddNumberedCourses(source, 3, CourseState.Archived);

        var byDefault = await RunAsync(source);
        var bySearch = await RunAsync(source, status: "all", q: "немає такого");

        Assert.Equal(CourseListEmptyState.NoMatch, Assert.IsType<CourseListResult>(byDefault.Page.List).EmptyState);
        Assert.Equal(CourseListEmptyState.NoMatch, Assert.IsType<CourseListResult>(bySearch.Page.List).EmptyState);
        Assert.Empty(source.PageRequests);
    }

    /// <summary>FR-011: a list with rows has no empty state, and no messages.</summary>
    [Fact]
    public async Task AListWithRows_HasNoEmptyState()
    {
        var source = new FakeCourseListSource();
        source.AddCourse("Фізика");

        var result = await RunAsync(source);

        Assert.Null(Assert.IsType<CourseListResult>(result.Page.List).EmptyState);
        Assert.Empty(result.Page.MessageKeys);
        Assert.Equal(0, source.AnyCourseStoredCalls);
    }

    /// <summary>
    /// AC-020, FR-016: page 1 and page 3 of 120 courses each make the same five reads — choice, count, page, teachers,
    /// student counts — one call each; teachers and counts are asked for the page's courses only.
    /// </summary>
    [Fact]
    public async Task TheNumberOfReads_IsFixed_AndTheSameForPage1AndPage3()
    {
        var first = new FakeCourseListSource();
        var firstIds = AddNumberedCourses(first, 120);
        foreach (var id in firstIds)
        {
            first.AddStudents(id, 2);
        }

        var third = new FakeCourseListSource();
        var thirdIds = AddNumberedCourses(third, 120);

        await RunAsync(first, page: "1");
        await RunAsync(third, page: "3");

        Assert.Equal(5, first.TotalCalls);
        Assert.Equal(5, third.TotalCalls);
        Assert.Equal(firstIds.Take(50).Order(), Assert.Single(first.TeacherRequests).Order());
        Assert.Equal(firstIds.Take(50).Order(), Assert.Single(first.StudentCountRequests).Order());
        Assert.Equal(thirdIds.Skip(100).Order(), Assert.Single(third.TeacherRequests).Order());
        Assert.Equal(thirdIds.Skip(100).Order(), Assert.Single(third.StudentCountRequests).Order());
    }

    /// <summary>AC-010, api-design §2.6: on 200 the return path is the canonical URL of the effective values and the page shown.</summary>
    [Fact]
    public async Task OnAListedPage_TheReturnPath_IsTheCanonicalUrl()
    {
        var source = new FakeCourseListSource();
        var teacher = source.AddPerson("Шевченко", "Тарас");
        foreach (var id in AddNumberedCourses(source, 60, CourseState.Archived))
        {
            source.AddTeacher(id, teacher);
        }

        var result = await RunAsync(source, "ARCHIVED", CourseListTestData.Id(teacher), " Курс ", "2");

        Assert.Equal(
            CourseListTestData.Path + "?status=archived&teacherId=" + CourseListTestData.Id(teacher) + "&q="
            + Uri.EscapeDataString("Курс") + "&page=2",
            result.Page.ReturnPath);
    }

    /// <summary>
    /// AC-014, FR-015, S-04: the query is a read — it takes no read-only guard, no write scope, no unit of work and no
    /// Google port, only the list's read port and the school's time zone.
    /// </summary>
    [Fact]
    public void TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly()
    {
        var parameters = typeof(GetCourseListQuery)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.Equal([typeof(ICourseListSource), typeof(SchoolTimeZone)], parameters);
    }
}
