using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-020 AC-002, AC-010, AC-013 (spec FR-009, FR-012, VR-001 … VR-005; api-design §2.4, §5): the defaults, every way a
/// parameter can be refused with its message key, 400 before 404, the rejected field back at its default while valid
/// fields keep their values, the teacher choice still read but no course read for the list, and the rejected value
/// nowhere in the page model or the return path.
/// </summary>
public sealed class CourseListRequestValidationTests
{
    private const string Secret = "zz-secret-zz";

    public static TheoryData<string> MalformedStatuses => new("foo", "1", " active", "active ", "act ive", "ACTIVE2", "всі", Secret);

    public static TheoryData<string, CourseStatusFilter> StatusTokens => new()
    {
        { "active", CourseStatusFilter.Active },
        { "archived", CourseStatusFilter.Archived },
        { "provisioned", CourseStatusFilter.Provisioned },
        { "declined", CourseStatusFilter.Declined },
        { "suspended", CourseStatusFilter.Suspended },
        { "all", CourseStatusFilter.All },
        { "ARCHIVED", CourseStatusFilter.Archived },
        { "All", CourseStatusFilter.All },
    };

    public static TheoryData<string> MalformedIds =>
        new("abc", "-1", "+1", "0", " 1", "1 ", "1.0", "1e3", "9223372036854775808", "٣", Secret);

    public static TheoryData<string> MalformedPages =>
        new("0", "-1", "+1", " 1", "1 ", "abc", "1.5", "100001", "99999999999999999999", "٣", Secret);

    public static TheoryData<string> ControlCharacterSearches => new("фіз\u0007ика", "a\tb", "a\u0000b", "a\u001Fb", "a\u007Fb");

    private static GetCourseListQuery QueryOver(FakeCourseListSource source) =>
        new(source, new SchoolTimeZone(CourseListTestData.Kyiv));

    private static Task<CourseListPageResult> RunAsync(
        FakeCourseListSource source,
        string?[]? status = null,
        string?[]? teacherId = null,
        string?[]? q = null,
        string?[]? page = null) =>
        QueryOver(source).ExecuteAsync(
            new CourseListRequest(status ?? [], teacherId ?? [], q ?? [], page ?? []),
            TestContext.Current.CancellationToken);

    /// <summary>A source with one current teacher of one active course; returns the teacher's id.</summary>
    private static (FakeCourseListSource Source, long Teacher) OneTeacher()
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse("Фізика");
        var teacher = source.AddPerson("Шевченко", "Тарас", "t.shevchenko@school.example");
        source.AddTeacher(course, teacher);
        return (source, teacher);
    }

    private static void AssertRefusedBeforeReading(CourseListPageResult result, FakeCourseListSource source)
    {
        Assert.Null(result.Page.List);
        Assert.True(source.NoCourseRead, "A course was read for the list although the query was refused.");
        Assert.Equal(1, source.TeacherChoiceCalls);
    }

    /// <summary>AC-002, VR-001 … VR-004: nothing given — ACTIVE, no teacher, no search, page 1, no message.</summary>
    [Fact]
    public async Task NoParameters_GiveTheDefaults()
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source);

        Assert.Equal(CourseListPageOutcome.Shown, result.Outcome);
        Assert.Equal(CourseStatusFilter.Active, result.Page.Status);
        Assert.Null(result.Page.SelectedTeacherId);
        Assert.Null(result.Page.SearchText);
        Assert.Empty(result.Page.MessageKeys);
        Assert.NotNull(result.Page.List);
        Assert.Equal(1, result.Page.List.Page);
        Assert.Equal(new CourseListFilter(CourseState.Active, null, null), Assert.Single(source.CountFilters));
        Assert.Equal(CourseListTestData.Path, result.Page.ReturnPath);
    }

    /// <summary>VR-001 … VR-004: an empty value is an absent one.</summary>
    [Fact]
    public async Task EmptyValues_AreAbsent()
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, [""], [""], [""], [""]);

        Assert.Equal(CourseListPageOutcome.Shown, result.Outcome);
        Assert.Equal(CourseStatusFilter.Active, result.Page.Status);
        Assert.Null(result.Page.SelectedTeacherId);
        Assert.Null(result.Page.SearchText);
        Assert.Equal(new CourseListFilter(CourseState.Active, null, null), Assert.Single(source.CountFilters));
    }

    /// <summary>VR-001, api-design §2.2: the six tokens, case-insensitive.</summary>
    [Theory]
    [MemberData(nameof(StatusTokens))]
    public async Task AStatusToken_IsAccepted_CaseInsensitively(string token, CourseStatusFilter expected)
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, status: [token]);

        Assert.Equal(CourseListPageOutcome.Shown, result.Outcome);
        Assert.Equal(expected, result.Page.Status);
    }

    /// <summary>AC-013, VR-001: anything else is malformed — 400 with StatusMalformed, the status back at ACTIVE.</summary>
    [Theory]
    [MemberData(nameof(MalformedStatuses))]
    public async Task AMalformedStatus_IsRefused(string status)
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, status: [status]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([CourseListMessageKey.StatusMalformed], result.Page.MessageKeys);
        Assert.Equal(CourseStatusFilter.Active, result.Page.Status);
        AssertRefusedBeforeReading(result, source);
    }

    /// <summary>AC-013, VR-001: a repeated status is malformed even when both values are valid.</summary>
    [Fact]
    public async Task ARepeatedStatus_IsRefused()
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, status: ["active", "active"]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([CourseListMessageKey.StatusMalformed], result.Page.MessageKeys);
        AssertRefusedBeforeReading(result, source);
    }

    /// <summary>AC-013, VR-002: a malformed teacher id — 400 with TeacherMalformed, the teacher back at "all".</summary>
    [Theory]
    [MemberData(nameof(MalformedIds))]
    public async Task AMalformedTeacherId_IsRefused(string teacherId)
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, teacherId: [teacherId]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([CourseListMessageKey.TeacherMalformed], result.Page.MessageKeys);
        Assert.Null(result.Page.SelectedTeacherId);
        AssertRefusedBeforeReading(result, source);
    }

    /// <summary>AC-013, VR-002: a repeated teacher id is malformed, even the same valid id twice.</summary>
    [Fact]
    public async Task ARepeatedTeacherId_IsRefused()
    {
        var (source, teacher) = OneTeacher();
        var id = CourseListTestData.Id(teacher);

        var result = await RunAsync(source, teacherId: [id, id]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([CourseListMessageKey.TeacherMalformed], result.Page.MessageKeys);
        AssertRefusedBeforeReading(result, source);
    }

    /// <summary>VR-002: the largest 64-bit id is well-formed; it is unknown here, so 404.</summary>
    [Fact]
    public async Task TheLargestId_IsWellFormed_AndUnknown()
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, teacherId: ["9223372036854775807"]);

        Assert.Equal(CourseListPageOutcome.TeacherUnknown, result.Outcome);
    }

    /// <summary>AC-013, VR-002, I-9: a well-formed id of nobody in the choice — 404 with TeacherUnknown; no course read.</summary>
    [Fact]
    public async Task AnUnknownTeacherId_IsRefusedWith404()
    {
        var (source, teacher) = OneTeacher();

        var result = await RunAsync(source, teacherId: [CourseListTestData.Id(teacher + 1000)]);

        Assert.Equal(CourseListPageOutcome.TeacherUnknown, result.Outcome);
        Assert.Equal([CourseListMessageKey.TeacherUnknown], result.Page.MessageKeys);
        Assert.Null(result.Page.SelectedTeacherId);
        Assert.Single(result.Page.TeacherChoice);
        AssertRefusedBeforeReading(result, source);
    }

    /// <summary>VR-002, FR-005: a former teacher (off the roster) and a student are not in the choice — both unknown.</summary>
    [Fact]
    public async Task AFormerTeacher_AndAStudent_AreUnknownTeachers()
    {
        var (source, _) = OneTeacher();
        var course = source.AddCourse("Хімія");
        var former = source.AddPerson("Коваль", "Олена");
        source.AddTeacher(course, former, onRoster: false);
        var student = source.AddPerson("Мельник", "Іван");
        source.AddStudent(course, student);

        var formerResult = await RunAsync(source, teacherId: [CourseListTestData.Id(former)]);
        var studentResult = await RunAsync(source, teacherId: [CourseListTestData.Id(student)]);

        Assert.Equal(CourseListPageOutcome.TeacherUnknown, formerResult.Outcome);
        Assert.Equal(CourseListPageOutcome.TeacherUnknown, studentResult.Outcome);
    }

    /// <summary>api-design §2.4: existence is checked only once the shape passes — a malformed status hides the unknown teacher.</summary>
    [Fact]
    public async Task AMalformedStatus_WithAnUnknownTeacher_Is400WithTheStatusKeyOnly()
    {
        var (source, teacher) = OneTeacher();

        var result = await RunAsync(source, status: ["foo"], teacherId: [CourseListTestData.Id(teacher + 1000)]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([CourseListMessageKey.StatusMalformed], result.Page.MessageKeys);
    }

    /// <summary>VR-003: white space only is no search; the text is trimmed.</summary>
    [Fact]
    public async Task TheSearchText_IsTrimmed_AndWhiteSpaceOnlyIsNoSearch()
    {
        var (source, _) = OneTeacher();

        var blank = await RunAsync(source, q: ["   "]);
        var trimmed = await RunAsync(source, q: ["  фізика  "]);

        Assert.Equal(CourseListPageOutcome.Shown, blank.Outcome);
        Assert.Null(blank.Page.SearchText);
        Assert.Equal(CourseListPageOutcome.Shown, trimmed.Outcome);
        Assert.Equal("фізика", trimmed.Page.SearchText);
        Assert.Equal(
            [new CourseListFilter(CourseState.Active, null, null), new CourseListFilter(CourseState.Active, null, "фізика")],
            source.CountFilters);
    }

    /// <summary>VR-003, I-4: 100 characters after trimming are accepted; 101 are too long.</summary>
    [Fact]
    public async Task OneHundredCharacters_AreAccepted_AndOneHundredAndOneAreTooLong()
    {
        var (source, _) = OneTeacher();
        var hundred = new string('я', 100);

        var accepted = await RunAsync(source, q: ["  " + hundred + "  "]);
        var refused = await RunAsync(source, q: [hundred + "я"]);

        Assert.Equal(CourseListPageOutcome.Shown, accepted.Outcome);
        Assert.Equal(hundred, accepted.Page.SearchText);
        Assert.Equal(CourseListPageOutcome.Invalid, refused.Outcome);
        Assert.Equal([CourseListMessageKey.SearchTooLong], refused.Page.MessageKeys);
        Assert.Null(refused.Page.SearchText);
    }

    /// <summary>VR-003: a control character (Unicode Cc) anywhere is refused with SearchControlCharacter.</summary>
    [Theory]
    [MemberData(nameof(ControlCharacterSearches))]
    public async Task AControlCharacter_IsRefused(string q)
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, q: [q]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([CourseListMessageKey.SearchControlCharacter], result.Page.MessageKeys);
        Assert.Null(result.Page.SearchText);
        AssertRefusedBeforeReading(result, source);
    }

    /// <summary>VR-003, FR-006: every other character is allowed — wildcards and quotes included.</summary>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    [InlineData("O'Neil")]
    [InlineData("10-А «фіз» ’")]
    public async Task OtherCharacters_AreAccepted(string q)
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, q: [q]);

        Assert.Equal(CourseListPageOutcome.Shown, result.Outcome);
        Assert.Equal(q, result.Page.SearchText);
    }

    /// <summary>api-design §2.4: a repeated search text is SearchRepeated, checked before the length and the characters.</summary>
    [Fact]
    public async Task ARepeatedSearchText_IsSearchRepeated_BeforeAnyOtherRule()
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, q: [new string('я', 101), "a\u0007"]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([CourseListMessageKey.SearchRepeated], result.Page.MessageKeys);
    }

    /// <summary>api-design §2.4: at most one key for q — too long is reported before a control character.</summary>
    [Fact]
    public async Task ATooLongSearchText_WithAControlCharacter_IsSearchTooLongOnly()
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, q: ["a\u0007" + new string('я', 100)]);

        Assert.Equal([CourseListMessageKey.SearchTooLong], result.Page.MessageKeys);
    }

    /// <summary>VR-004, I-5: 1 and 100 000 are the bounds.</summary>
    [Theory]
    [InlineData("1")]
    [InlineData("100000")]
    [InlineData("007")]
    public async Task APageNumberInRange_IsAccepted(string page)
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, page: [page]);

        Assert.Equal(CourseListPageOutcome.Shown, result.Outcome);
        Assert.Empty(result.Page.MessageKeys);
    }

    /// <summary>AC-013, VR-004: anything else is PageMalformed.</summary>
    [Theory]
    [MemberData(nameof(MalformedPages))]
    public async Task AMalformedPage_IsRefused(string page)
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, page: [page]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal([CourseListMessageKey.PageMalformed], result.Page.MessageKeys);
        AssertRefusedBeforeReading(result, source);
    }

    /// <summary>AC-013, VR-004: a repeated page is malformed.</summary>
    [Fact]
    public async Task ARepeatedPage_IsRefused()
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, page: ["1", "2"]);

        Assert.Equal([CourseListMessageKey.PageMalformed], result.Page.MessageKeys);
    }

    /// <summary>FR-012, api-design §2.4: every failing parameter is reported, in the order status, teacherId, q, page.</summary>
    [Fact]
    public async Task EveryMalformedParameter_IsReported_InParameterOrder()
    {
        var (source, _) = OneTeacher();

        var result = await RunAsync(source, ["foo"], ["x"], [new string('я', 101)], ["0"]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal(
            [
                CourseListMessageKey.StatusMalformed, CourseListMessageKey.TeacherMalformed,
                CourseListMessageKey.SearchTooLong, CourseListMessageKey.PageMalformed,
            ],
            result.Page.MessageKeys);
        AssertRefusedBeforeReading(result, source);
    }

    /// <summary>FR-012: the rejected field shows its default; the valid ones keep their values.</summary>
    [Fact]
    public async Task OnARefusal_ValidFieldsKeepTheirValues()
    {
        var (source, teacher) = OneTeacher();

        var result = await RunAsync(source, ["archived"], [CourseListTestData.Id(teacher)], ["фіз"], ["abc"]);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.Equal(CourseStatusFilter.Archived, result.Page.Status);
        Assert.Equal(teacher, result.Page.SelectedTeacherId);
        Assert.Equal("фіз", result.Page.SearchText);
    }

    /// <summary>FR-005, api-design §2.5: the teacher choice is in the model on 200, 400 and 404 alike.</summary>
    [Fact]
    public async Task TheTeacherChoice_IsInTheModel_OnEveryAnswer()
    {
        var (source, teacher) = OneTeacher();

        var shown = await RunAsync(source);
        var invalid = await RunAsync(source, status: ["foo"]);
        var unknown = await RunAsync(source, teacherId: [CourseListTestData.Id(teacher + 1000)]);

        foreach (var result in new[] { shown, invalid, unknown })
        {
            Assert.Equal(teacher, Assert.Single(result.Page.TeacherChoice).ParticipantId);
        }
    }

    /// <summary>AC-010, FR-009, api-design §2.6: after a refusal the return path omits every rejected parameter and the page.</summary>
    [Fact]
    public async Task AfterARefusal_TheReturnPath_OmitsTheRejectedParameters_AndThePage()
    {
        var (source, teacher) = OneTeacher();

        var badStatus = await RunAsync(source, ["foo"], [CourseListTestData.Id(teacher)], ["фіз"], ["2"]);
        var badTeacher = await RunAsync(source, ["archived"], ["x"], [], ["2"]);
        var unknownTeacher = await RunAsync(source, ["all"], [CourseListTestData.Id(teacher + 1000)], [], ["2"]);
        var badPage = await RunAsync(source, ["archived"], [], [], ["0"]);

        Assert.Equal(
            CourseListTestData.Path + "?teacherId=" + CourseListTestData.Id(teacher) + "&q=" + Uri.EscapeDataString("фіз"),
            badStatus.Page.ReturnPath);
        Assert.Equal(CourseListTestData.Path + "?status=archived", badTeacher.Page.ReturnPath);
        Assert.Equal(CourseListTestData.Path + "?status=all", unknownTeacher.Page.ReturnPath);
        Assert.Equal(CourseListTestData.Path + "?status=archived", badPage.Page.ReturnPath);
    }

    /// <summary>AC-013, S-07: no rejected value appears anywhere in the page model.</summary>
    [Fact]
    public async Task ARejectedValue_AppearsNowhereInTheModel()
    {
        var (source, _) = OneTeacher();
        var longSecret = Secret + new string('я', 100);

        var result = await RunAsync(source, [Secret], [Secret], [longSecret], [Secret]);
        var json = JsonSerializer.Serialize(result.Page);

        Assert.Equal(CourseListPageOutcome.Invalid, result.Outcome);
        Assert.DoesNotContain("zz-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-secret", result.Page.ReturnPath, StringComparison.Ordinal);
    }
}
