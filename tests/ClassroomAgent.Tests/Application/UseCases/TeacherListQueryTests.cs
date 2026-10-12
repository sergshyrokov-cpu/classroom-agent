using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-022 spec FR-002 … FR-010, VR-001, VR-002, entity model §3, §4 (AC-001 … AC-006, AC-008, AC-013, AC-015): the state
/// handed to the port, the row built from the port's record, the course-list link, paging with the page past the end,
/// the empty state, the return path, validation before any read, and at most two reads per request. The port is the
/// in-memory <see cref="FakeTeacherListSource"/>; filtering and ordering in the database are proved against PostgreSQL
/// in <c>TeacherListSourceTests</c>.
/// </summary>
public sealed class TeacherListQueryTests
{
    public static TheoryData<string, CourseState?> StatusTokens => new()
    {
        { "active", CourseState.Active },
        { "archived", CourseState.Archived },
        { "provisioned", CourseState.Provisioned },
        { "declined", CourseState.Declined },
        { "suspended", CourseState.Suspended },
        { "all", null },
        { "ARCHIVED", CourseState.Archived },
        { "All", null },
    };

    public static TheoryData<string?[]> MalformedStatuses => new()
    {
        new string?[] { "unknown" },
        new string?[] { "1" },
        new string?[] { " active" },
        new string?[] { "active " },
        new string?[] { "act ive" },
        new string?[] { "active", "archived" },
        new string?[] { "active", "active" },
    };

    public static TheoryData<string?[]> MalformedPages => new()
    {
        new string?[] { "0" },
        new string?[] { "-1" },
        new string?[] { "+1" },
        new string?[] { " 1" },
        new string?[] { "1 " },
        new string?[] { "abc" },
        new string?[] { "1.0" },
        new string?[] { "100001" },
        new string?[] { "99999999999" },
        new string?[] { "١" },
        new string?[] { "1", "2" },
    };

    private static Task<TeacherListPageResult> RunAsync(FakeTeacherListSource source, string? status = null, string? page = null) =>
        RunRawAsync(source, One(status), One(page));

    private static Task<TeacherListPageResult> RunRawAsync(FakeTeacherListSource source, string?[] status, string?[] page) =>
        new GetTeacherListQuery(source).ExecuteAsync(
            new TeacherListRequest(status, page), TestContext.Current.CancellationToken);

    private static string?[] One(string? value) => value is null ? [] : [value];

    /// <summary>AC-001, FR-005, OD-003: with no status the port is asked for ACTIVE, and ACTIVE is the effective status.</summary>
    [Fact]
    public async Task ByDefault_ThePortIsAskedForActive()
    {
        var source = new FakeTeacherListSource();
        source.AddTeacher("Шевченко", "Тарас");

        var result = await RunAsync(source);

        Assert.Equal(TeacherListPageOutcome.Shown, result.Outcome);
        Assert.Equal(CourseStatusFilter.Active, result.Page.Status);
        Assert.Equal([CourseState.Active], source.CountRequests);
        Assert.Equal(CourseState.Active, Assert.Single(source.PageRequests).State);
    }

    /// <summary>VR-001: an empty status is absent — ACTIVE, not an error.</summary>
    [Fact]
    public async Task AnEmptyStatusAndPage_AreTheDefaults()
    {
        var source = new FakeTeacherListSource();
        source.AddTeacher("Шевченко", "Тарас");

        var result = await RunAsync(source, string.Empty, string.Empty);

        Assert.Equal(TeacherListPageOutcome.Shown, result.Outcome);
        Assert.Equal(CourseStatusFilter.Active, result.Page.Status);
        Assert.Equal(1, Assert.IsType<TeacherListResult>(result.Page.List).Page);
    }

    /// <summary>AC-002, FR-005, VR-001: each token, in any case, selects its state; "all" asks for no state.</summary>
    [Theory]
    [MemberData(nameof(StatusTokens))]
    public async Task EachStatusToken_IsHandedToThePort(string token, CourseState? expected)
    {
        var source = new FakeTeacherListSource();
        source.AddTeacher("Шевченко", "Тарас", active: 1, archived: 1, provisioned: 1, declined: 1, suspended: 1);

        var result = await RunAsync(source, token);

        Assert.Equal(TeacherListPageOutcome.Shown, result.Outcome);
        Assert.Equal([expected], source.CountRequests);
        Assert.Equal(expected, Assert.Single(source.PageRequests).State);
        Assert.Equal(token.ToLowerInvariant(), CourseListUrl.Token(result.Page.Status));
    }

    /// <summary>AC-003, FR-003, FR-004: the row carries the profile name, the email as stored and the port's course count.</summary>
    [Fact]
    public async Task TheRow_CarriesTheNameTheEmailAndTheCount()
    {
        var source = new FakeTeacherListSource();
        var id = source.AddTeacher("Шевченко", "Тарас", "T.Shevchenko@School.example", active: 2);

        var result = await RunAsync(source);

        var row = Assert.Single(Assert.IsType<TeacherListResult>(result.Page.List).Rows);
        Assert.Equal(id, row.ParticipantId);
        Assert.Equal(new CourseListPersonName(CourseListPersonNameKind.ProfileName, "Шевченко Тарас"), row.Name);
        Assert.Equal("T.Shevchenko@School.example", row.Email);
        Assert.Equal(2, row.CourseCount);
    }

    /// <summary>
    /// AC-003, FR-004: one part alone, the email when there is no name (shown in both columns, I-3), no name at all; an
    /// empty string is absent; an empty email is null.
    /// </summary>
    [Fact]
    public async Task TheNameRule_FallsBackToTheEmail_ThenToNoName()
    {
        var source = new FakeTeacherListSource();
        var surnameOnly = source.AddTeacher("Гнатюк", string.Empty, null);
        var givenOnly = source.AddTeacher(null, "Олег", string.Empty);
        var emailOnly = source.AddTeacher(string.Empty, null, "zoe@school.example");
        var nothing = source.AddTeacher(null, string.Empty, string.Empty);

        var result = await RunAsync(source);

        var rows = Assert.IsType<TeacherListResult>(result.Page.List).Rows.ToDictionary(r => r.ParticipantId);
        Assert.Equal(new CourseListPersonName(CourseListPersonNameKind.ProfileName, "Гнатюк"), rows[surnameOnly].Name);
        Assert.Null(rows[surnameOnly].Email);
        Assert.Equal(new CourseListPersonName(CourseListPersonNameKind.ProfileName, "Олег"), rows[givenOnly].Name);
        Assert.Null(rows[givenOnly].Email);
        Assert.Equal(new CourseListPersonName(CourseListPersonNameKind.Email, "zoe@school.example"), rows[emailOnly].Name);
        Assert.Equal("zoe@school.example", rows[emailOnly].Email);
        Assert.Equal(new CourseListPersonName(CourseListPersonNameKind.NoName, null), rows[nothing].Name);
        Assert.Null(rows[nothing].Email);
    }

    /// <summary>AC-014, FR-006: the rows keep the port's order.</summary>
    [Fact]
    public async Task TheRows_KeepThePortsOrder()
    {
        var source = new FakeTeacherListSource();
        var ivanenko = source.AddTeacher("Іваненко", "Ірина");
        var hnatiuk = source.AddTeacher("Гнатюк", "Олег");
        var gudz = source.AddTeacher("Ґудзь", "Анна");
        var andriienko = source.AddTeacher("Андрієнко", "Петро");
        var zoe = source.AddTeacher(email: "zoe@school.example");
        var nobody = source.AddTeacher();

        var result = await RunAsync(source);

        Assert.Equal(
            [andriienko, hnatiuk, gudz, ivanenko, zoe, nobody],
            Assert.IsType<TeacherListResult>(result.Page.List).Rows.Select(r => r.ParticipantId));
    }

    /// <summary>
    /// AC-004, AC-006, FR-007, api-design §2.6: the row links to the course list with the effective status and the
    /// participant id only — ACTIVE omitted, page 1, no search.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("active", null)]
    [InlineData("archived", "archived")]
    [InlineData("SUSPENDED", "suspended")]
    [InlineData("all", "all")]
    public async Task TheRowLink_IsTheCourseListOfThatTeacherAndStatus(string? status, string? linkStatus)
    {
        var source = new FakeTeacherListSource();
        var id = source.AddTeacher("Шевченко", "Тарас", "t@school.example", active: 1, archived: 1, suspended: 1);

        var result = await RunAsync(source, status);

        var row = Assert.Single(Assert.IsType<TeacherListResult>(result.Page.List).Rows);
        Assert.Equal(TeacherListTestData.CoursesLink(id, linkStatus), row.CoursesUrl);
    }

    /// <summary>AC-005, AC-015, FR-006: 120 teachers — 50 per page, page 2 is 51–100, page 3 is 101–120, three pages.</summary>
    [Fact]
    public async Task Paging_Is50PerPage_FromThePort()
    {
        var source = new FakeTeacherListSource();
        var ids = source.AddNumberedTeachers(120);

        var second = await RunAsync(source, page: "2");
        var third = await RunAsync(source, page: "3");

        var list2 = Assert.IsType<TeacherListResult>(second.Page.List);
        Assert.Equal(ids.Skip(50).Take(50), list2.Rows.Select(r => r.ParticipantId));
        Assert.Equal((120, 2, 3, 50), (list2.TotalCount, list2.Page, list2.PageCount, list2.PageSize));
        Assert.False(list2.IsEmpty);
        var list3 = Assert.IsType<TeacherListResult>(third.Page.List);
        Assert.Equal(ids.Skip(100), list3.Rows.Select(r => r.ParticipantId));
        Assert.Equal(3, list3.Page);
        Assert.Contains((CourseState.Active, 50, 50), source.PageRequests);
        Assert.Contains((CourseState.Active, 100, 50), source.PageRequests);
        Assert.Equal(50, GetTeacherListQuery.PageSize);
    }

    /// <summary>FR-006, VR-002: a page past the last shows the last; the largest accepted number is 100 000.</summary>
    [Theory]
    [InlineData("4")]
    [InlineData("100000")]
    public async Task APagePastTheLast_ShowsTheLast(string page)
    {
        var source = new FakeTeacherListSource();
        var ids = source.AddNumberedTeachers(120);

        var result = await RunAsync(source, "all", page);

        Assert.Equal(TeacherListPageOutcome.Shown, result.Outcome);
        var list = Assert.IsType<TeacherListResult>(result.Page.List);
        Assert.Equal(3, list.Page);
        Assert.Equal(ids.Skip(100), list.Rows.Select(r => r.ParticipantId));
        Assert.Equal((null, 100, 50), Assert.Single(source.PageRequests));
        Assert.Equal("/workspace/teachers?status=all&page=3", result.Page.ReturnPath);
    }

    /// <summary>AC-008, FR-008, I-2: nobody listed — the empty state on page 1, no page read.</summary>
    [Fact]
    public async Task NobodyListed_IsTheEmptyState_WithoutAPageRead()
    {
        var source = new FakeTeacherListSource();
        source.AddTeacher("Коваль", "Олена", active: 0, archived: 1);

        var result = await RunAsync(source, page: "5");

        Assert.Equal(TeacherListPageOutcome.Shown, result.Outcome);
        var list = Assert.IsType<TeacherListResult>(result.Page.List);
        Assert.True(list.IsEmpty);
        Assert.Equal((0, 1, 1), (list.TotalCount, list.Page, list.PageCount));
        Assert.Empty(list.Rows);
        Assert.Empty(source.PageRequests);
        Assert.Equal(1, source.TotalCalls);
        Assert.Equal("/workspace/teachers", result.Page.ReturnPath);
    }

    /// <summary>AC-015, FR-014: a shown page reads the count and one page — two calls, whatever the size or page.</summary>
    [Theory]
    [InlineData(3, "1")]
    [InlineData(120, "1")]
    [InlineData(120, "3")]
    public async Task AShownPage_ReadsTwice(int teachers, string page)
    {
        var source = new FakeTeacherListSource();
        source.AddNumberedTeachers(teachers);

        await RunAsync(source, "all", page);

        Assert.Single(source.CountRequests);
        Assert.Equal(50, Assert.Single(source.PageRequests).Limit);
    }

    /// <summary>FR-009, S-06: the return path is the teacher-list URL of the effective status and the page shown.</summary>
    [Fact]
    public async Task TheReturnPath_IsTheValidatedUrl()
    {
        var source = new FakeTeacherListSource();
        source.AddNumberedTeachers(60);
        source.AddTeacher("Коваль", "Олена", active: 0, archived: 1);

        var archived = await RunAsync(source, "ARCHIVED");
        var second = await RunAsync(source, page: "2");

        Assert.Equal("/workspace/teachers?status=archived", archived.Page.ReturnPath);
        Assert.Equal("/workspace/teachers?page=2", second.Page.ReturnPath);
    }

    /// <summary>AC-013, VR-001, FR-010: a malformed status — Invalid, its message, ACTIVE in its place, nothing read.</summary>
    [Theory]
    [MemberData(nameof(MalformedStatuses))]
    public async Task AMalformedStatus_IsRefused_BeforeAnyRead(string?[] status)
    {
        var source = new FakeTeacherListSource();
        source.AddTeacher("Шевченко", "Тарас");

        var result = await RunRawAsync(source, status, []);

        Assert.Equal(TeacherListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([TeacherListMessageKey.StatusMalformed], result.Page.MessageKeys);
        Assert.Equal(CourseStatusFilter.Active, result.Page.Status);
        Assert.Null(result.Page.List);
        Assert.Equal(0, source.TotalCalls);
    }

    /// <summary>AC-013, VR-002, FR-010: a malformed page — Invalid, its message, nothing read.</summary>
    [Theory]
    [MemberData(nameof(MalformedPages))]
    public async Task AMalformedPage_IsRefused_BeforeAnyRead(string?[] page)
    {
        var source = new FakeTeacherListSource();
        source.AddTeacher("Шевченко", "Тарас");

        var result = await RunRawAsync(source, [], page);

        Assert.Equal(TeacherListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([TeacherListMessageKey.PageMalformed], result.Page.MessageKeys);
        Assert.Null(result.Page.List);
        Assert.Equal(0, source.TotalCalls);
    }

    /// <summary>FR-010: both malformed — both messages, in parameter order.</summary>
    [Fact]
    public async Task BothMalformed_GiveBothMessages_InOrder()
    {
        var source = new FakeTeacherListSource();

        var result = await RunAsync(source, "foo", "0");

        Assert.Equal(TeacherListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([TeacherListMessageKey.StatusMalformed, TeacherListMessageKey.PageMalformed], result.Page.MessageKeys);
        Assert.Equal(0, source.TotalCalls);
    }

    /// <summary>
    /// AC-013, FR-010, S-06: after a refusal the rejected field shows its default, the valid one stays, and the return
    /// path is page 1 of the effective status — the rejected value is nowhere in the model.
    /// </summary>
    [Fact]
    public async Task AfterARefusal_TheReturnPathDropsTheRejectedValue()
    {
        var source = new FakeTeacherListSource();

        var badStatus = await RunAsync(source, "zz-secret-zz", "2");
        var badPage = await RunAsync(source, "archived", "zz-secret-zz");

        Assert.Equal("/workspace/teachers", badStatus.Page.ReturnPath);
        Assert.Equal(CourseStatusFilter.Archived, badPage.Page.Status);
        Assert.Equal("/workspace/teachers?status=archived", badPage.Page.ReturnPath);
        Assert.DoesNotContain("zz-secret-zz", badStatus.Page.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("zz-secret-zz", badPage.Page.ToString(), StringComparison.Ordinal);
    }
}
