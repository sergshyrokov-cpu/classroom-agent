using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-071 FR-002 … FR-008, VR-001 … VR-003, api-design D-2: the query with its ports substituted (TC-1): the three states, what
/// is read in each, the rows' mapping with the BR-068 mark in a non-UTC school zone (TC-8), the totals and the return path.
/// </summary>
public sealed class GetSearchResultsQueryTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly DateTimeOffset LateEvening = new(2026, 9, 10, 22, 30, 0, TimeSpan.Zero);

    private readonly FakeStudentSearchSource _students = new();

    private readonly FakeTeacherListSource _teachers = new();

    private readonly FakeCourseListSource _courses = new();

    private readonly FakeMeetCodesSearchSource _codes = new();

    private GetSearchResultsQuery Query() =>
        new(_students, _teachers, _courses, _codes, new SchoolTimeZone(JournalTestData.Kyiv));

    private Task<SearchResultsPageModel> SearchAsync(params string?[] q) =>
        Query().ExecuteAsync(new SearchResultsRequest(q), Ct);

    private void AssertNothingRead()
    {
        Assert.Empty(_students.Calls);
        Assert.Equal(0, _teachers.TotalCalls);
        Assert.Empty(_courses.CountFilters);
        Assert.Empty(_courses.PageRequests);
        Assert.Empty(_codes.Calls);
    }

    private static void AssertNoGroup(SearchResultsPageModel page)
    {
        Assert.Empty(page.Students.Rows);
        Assert.Equal(0, page.Students.Total);
        Assert.Empty(page.Teachers.Rows);
        Assert.Equal(0, page.Teachers.Total);
        Assert.Empty(page.Courses.Rows);
        Assert.Equal(0, page.Courses.Total);
        Assert.Empty(page.MeetCodes.Rows);
        Assert.Equal(0, page.MeetCodes.Total);
    }

    public static TheoryData<string?[], SearchRefusalKey> Refusals => new()
    {
        { ["аб", "вг"], SearchRefusalKey.SearchRepeated },
        { [new string('я', 101)], SearchRefusalKey.SearchTooLong },
        { ["аб\u0007"], SearchRefusalKey.SearchControlCharacter },
    };

    /// <summary>VR-002, AC-005: a refused text reads nothing, keeps no text and its return path holds none.</summary>
    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task ARefusedText_ReadsNothing_AndKeepsNoText(string?[] q, SearchRefusalKey key)
    {
        var page = await SearchAsync(q);

        Assert.Equal(SearchResultsState.Refused, page.State);
        Assert.Equal(key, page.Refusal);
        Assert.Null(page.SearchText);
        Assert.Equal(SearchResultsUrl.Path, page.ReturnPath);
        AssertNoGroup(page);
        AssertNothingRead();
    }

    public static TheoryData<string?[]> NoTexts => new(Array.Empty<string?>(), new string?[] { string.Empty }, new string?[] { "   " });

    /// <summary>FR-008, VR-001: no text, or only white space, is the prompt: nothing read, no text.</summary>
    [Theory]
    [MemberData(nameof(NoTexts))]
    public async Task NoText_IsThePrompt_AndReadsNothing(string?[] q)
    {
        var page = await SearchAsync(q);

        Assert.Equal(SearchResultsState.Prompt, page.State);
        Assert.Null(page.Refusal);
        Assert.Null(page.SearchText);
        Assert.Equal(SearchResultsUrl.Path, page.ReturnPath);
        AssertNoGroup(page);
        AssertNothingRead();
    }

    /// <summary>VR-003: one character after trimming is the prompt, nothing read, and the text stays with its return path.</summary>
    [Theory]
    [InlineData("я")]
    [InlineData("  я  ")]
    public async Task OneCharacter_IsThePrompt_AndTheTextStays(string q)
    {
        var page = await SearchAsync(q);

        Assert.Equal(SearchResultsState.Prompt, page.State);
        Assert.Equal("я", page.SearchText);
        Assert.Equal(SearchResultsUrl.Build("я"), page.ReturnPath);
        AssertNothingRead();
    }

    /// <summary>VR-003: two characters, and two one-letter words (3 characters), are searched, trimmed but whole.</summary>
    [Theory]
    [InlineData("аб", "аб")]
    [InlineData("а б", "а б")]
    [InlineData("  аб  ", "аб")]
    [InlineData("а   б", "а   б")]
    public async Task FromTwoCharacters_TheTextIsSearched_Whole(string q, string searched)
    {
        var page = await SearchAsync(q);

        Assert.Equal(SearchResultsState.Results, page.State);
        Assert.Equal(searched, page.SearchText);
        Assert.Equal(SearchResultsUrl.Build(searched), page.ReturnPath);
        Assert.Contains(nameof(FakeStudentSearchSource.CountStudentsAsync), _students.Calls);
        Assert.Equal([searched], _codes.CountReads);
    }

    /// <summary>D-2, db-design §5: nothing found is Results with four zero totals; a group with total 0 skips its rows.</summary>
    [Fact]
    public async Task NothingFound_IsResultsWithFourZeroTotals_AndReadsNoRows()
    {
        var page = await SearchAsync("аб");

        Assert.Equal(SearchResultsState.Results, page.State);
        AssertNoGroup(page);
        Assert.Null(page.MeetCodes.FirstListWithMatch);
        Assert.Equal([nameof(FakeStudentSearchSource.CountStudentsAsync)], _students.Calls);
        Assert.Empty(_teachers.PageRequests);
        Assert.Empty(_courses.PageRequests);
        Assert.Equal([nameof(FakeMeetCodesSearchSource.GetCountsAsync)], _codes.Calls);
    }

    /// <summary>FR-004, FR-005, I-6: teachers and courses are read at status "all", with the text and the group's limit.</summary>
    [Fact]
    public async Task TeachersAndCourses_AreReadAtStatusAll_WithTheLimit()
    {
        _teachers.AddTeacher("Коваль", "Ірина", "koval@example.test", active: 0, archived: 1);
        _courses.AddCourse("абетка", state: CourseState.Archived);

        var page = await SearchAsync("аб");

        var (state, offset, limit) = Assert.Single(_teachers.PageRequests);
        Assert.Null(state);
        Assert.Equal((0, GetSearchResultsQuery.GroupSize), (offset, limit));
        var filter = Assert.Single(_courses.CountFilters);
        Assert.Equal(new CourseListFilter(null, null, "аб"), filter);
        var read = Assert.Single(_courses.PageRequests);
        Assert.Equal((filter, 0, GetSearchResultsQuery.GroupSize), read);
        Assert.Equal(1, page.Teachers.Total);
        Assert.Equal(1, page.Courses.Total);
    }

    /// <summary>FR-004: a teacher row takes the course list's name rule: name, else email; an empty email is none.</summary>
    [Fact]
    public async Task ATeacherRow_HasTheLabelAndEmail()
    {
        var named = _teachers.AddTeacher("Коваль", "Ірина", "koval@example.test");
        var byEmail = _teachers.AddTeacher(null, null, "email.only@example.test");
        var noEmail = _teachers.AddTeacher("Шевчук", "Олена", string.Empty);

        var page = await SearchAsync("аб");

        Assert.Contains(new TeacherSearchRow(named, "Коваль Ірина", "koval@example.test"), page.Teachers.Rows);
        Assert.Contains(new TeacherSearchRow(byEmail, "email.only@example.test", "email.only@example.test"), page.Teachers.Rows);
        Assert.Contains(new TeacherSearchRow(noEmail, "Шевчук Олена", null), page.Teachers.Rows);
    }

    /// <summary>FR-005: a course row carries the name, the section and the state of the course list.</summary>
    [Fact]
    public async Task ACourseRow_HasNameSectionAndState()
    {
        var id = _courses.AddCourse("Абетка", "10-А", CourseState.Archived);

        var page = await SearchAsync("аб");

        Assert.Equal(new CourseSearchRow(id, "Абетка", "10-А", CourseState.Archived), Assert.Single(page.Courses.Rows));
    }

    /// <summary>FR-003: a student row has the label, the email and all their courses in the order the port gives.</summary>
    [Fact]
    public async Task AStudentRow_HasTheLabelEmailAndCourses_InTheGivenOrder()
    {
        _students.Students.Add(new StudentSearchRecord(7, "Мельник", "Петро", "m@example.test"));
        _students.Students.Add(new StudentSearchRecord(8, null, null, "email.only@example.test"));
        _students.Courses.Add(new StudentSearchCourseRecord(7, 20, "Алгебра", null, true, false, LateEvening));
        _students.Courses.Add(new StudentSearchCourseRecord(8, 30, "Біологія", "9-Б", true, false, LateEvening));
        _students.Courses.Add(new StudentSearchCourseRecord(7, 10, "Ядро", "10-А", true, false, LateEvening));

        var page = await SearchAsync("аб");

        Assert.Equal(2, page.Students.Total);
        var melnyk = page.Students.Rows[0];
        Assert.Equal((7L, "Мельник Петро", "m@example.test"), (melnyk.ParticipantId, melnyk.Label, melnyk.Email));
        Assert.Equal(
            [new StudentSearchCourse(20, "Алгебра", null, null), new StudentSearchCourse(10, "Ядро", "10-А", null)],
            melnyk.Courses);
        var byEmail = page.Students.Rows[1];
        Assert.Equal("email.only@example.test", byEmail.Label);
        Assert.Equal([new StudentSearchCourse(30, "Біологія", "9-Б", null)], byEmail.Courses);
        Assert.Equal([7L, 8L], Assert.Single(_students.CourseReads).Order());
    }

    /// <summary>
    /// FR-003, BR-068, US-060 OD-003 a, TC-8: off the roster carries the last-seen date of the school's zone (22:30 UTC is the next
    /// day in Kyiv); equal sightings carry no date; on the roster, no mark, whatever the sightings.
    /// </summary>
    [Fact]
    public async Task TheLeftMark_FollowsTheJournalsRule_InTheSchoolsZone()
    {
        _students.Students.Add(new StudentSearchRecord(7, "Мельник", "Петро", null));
        _students.Courses.Add(new StudentSearchCourseRecord(7, 1, "Пішов", null, false, false, LateEvening));
        _students.Courses.Add(new StudentSearchCourseRecord(7, 2, "Лише робота", null, false, true, LateEvening));
        _students.Courses.Add(new StudentSearchCourseRecord(7, 3, "На місці", null, true, false, LateEvening));
        _students.Courses.Add(new StudentSearchCourseRecord(7, 4, "На місці одразу", null, true, true, LateEvening));

        var page = await SearchAsync("аб");

        var marks = Assert.Single(page.Students.Rows).Courses.ToDictionary(c => c.CourseId, c => c.Left);
        Assert.Equal(new LeftMark(new DateOnly(2026, 9, 11)), marks[1]);
        Assert.Equal(new LeftMark(null), marks[2]);
        Assert.Null(marks[3]);
        Assert.Null(marks[4]);
    }

    /// <summary>FR-007: rows up to the limit with the total of all matches, and the courses are read only for the shown students.</summary>
    [Fact]
    public async Task TheStudentsGroup_ShowsTheLimit_WithTheTotal_AndReadsCoursesOfTheShownOnly()
    {
        for (var i = 1L; i <= 25; i++)
        {
            _students.Students.Add(new StudentSearchRecord(i, "Прізвище" + i, null, null));
        }

        var page = await SearchAsync("аб");

        Assert.Equal(25, page.Students.Total);
        Assert.Equal(GetSearchResultsQuery.GroupSize, page.Students.Rows.Count);
        Assert.Equal(("аб", 20), Assert.Single(_students.RowReads));
        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), Assert.Single(_students.CourseReads).Order());
    }

    /// <summary>Section 9: the courses of the students are read once, however many students are shown.</summary>
    [Fact]
    public async Task TheCoursesOfTheStudents_AreReadOnce()
    {
        _students.Students.Add(new StudentSearchRecord(1, "А", null, null));
        _students.Students.Add(new StudentSearchRecord(2, "Б", null, null));
        _students.Students.Add(new StudentSearchRecord(3, "В", null, null));

        await SearchAsync("аб");

        Assert.Single(_students.CourseReads);
    }

    public static TheoryData<int, int, int, MeetCodeList> FirstLists => new()
    {
        { 1, 1, 1, MeetCodeList.Unassigned },
        { 0, 2, 1, MeetCodeList.Linked },
        { 0, 0, 3, MeetCodeList.NotACourse },
    };

    /// <summary>FR-006, FR-007, I-11: the total is the sum of the three counters and the link's list the first with a match.</summary>
    [Theory]
    [MemberData(nameof(FirstLists))]
    public async Task TheMeetGroup_SumsTheCounters_AndNamesTheFirstListWithAMatch(int unassigned, int linked, int marked, MeetCodeList first)
    {
        _codes.Counts = new MeetCodeListCounts(unassigned, linked, marked);
        _codes.Matches.Add(new MeetCodeMatch("aaa-bbbb-ccc", first));

        var page = await SearchAsync("аб");

        Assert.Equal(unassigned + linked + marked, page.MeetCodes.Total);
        Assert.Equal(first, page.MeetCodes.FirstListWithMatch);
        Assert.Equal([new MeetCodeMatch("aaa-bbbb-ccc", first)], page.MeetCodes.Rows);
        Assert.Equal(("аб", 20), Assert.Single(_codes.MatchReads));
    }

    /// <summary>x-return-path: with a text, the encoded text; with none, the bare path.</summary>
    [Theory]
    [InlineData(null, "/workspace/search")]
    [InlineData("", "/workspace/search")]
    [InlineData("а б", "/workspace/search?q=%D0%B0%20%D0%B1")]
    [InlineData("a&b=<c>\"d'", "/workspace/search?q=a%26b%3D%3Cc%3E%22d%27")]
    public void TheUrl_EncodesTheText(string? text, string expected) =>
        Assert.Equal(expected, SearchResultsUrl.Build(text));

    /// <summary>D-12: the path equals the host's route; the two numbers of the Specification are one constant each.</summary>
    [Fact]
    public void TheConstants_AreTheSpecifications()
    {
        Assert.Equal("/workspace/search", SearchResultsUrl.Path);
        Assert.Equal(20, GetSearchResultsQuery.GroupSize);
        Assert.Equal(2, GetSearchResultsQuery.MinimumLength);
    }
}
