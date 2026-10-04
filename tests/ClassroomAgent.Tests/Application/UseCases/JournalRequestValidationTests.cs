using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-025 AC-001, AC-005, AC-011 (spec FR-001, VR-001 … VR-004, §6): the journal request — the form without a course,
/// the course drop-down, the <c>view</c> parameter and every way a parameter can be refused, with the messages in
/// order and no data read once the request is refused.
/// </summary>
public sealed class JournalRequestValidationTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk");

    public static TheoryData<string> MalformedCourseIds =>
        new("abc", "-1", "+1", " 1", "1 ", "0", "1.0", "9223372036854775808", "١");

    public static TheoryData<string> MalformedDates =>
        new("2026-9-1", "2026-09-31", "2027-02-29", "01.09.2026", "1999-12-31", "2101-01-01", " 2026-09-01", "2026-09-01T00:00");

    public static TheoryData<string> BoundaryDates => new("2000-01-01", "2100-12-31", "2028-02-29");

    private static GetJournalQuery QueryOver(FakeJournalSource source) =>
        new(source, new SchoolTimeZone(JournalTestData.Kyiv), new ManualTimeProvider(InstallationTestHost.DefaultStart));

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static Task<JournalPageResult> RunAsync(FakeJournalSource source, JournalRequest request) =>
        QueryOver(source).ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);

    private static JournalRequest September(long courseId) =>
        new([Id(courseId)], [JournalTestData.Period.FromText], [JournalTestData.Period.ToText], []);

    private static (FakeJournalSource Source, long Course) OneCourse()
    {
        var source = new FakeJournalSource();
        return (source, source.AddCourse("Course"));
    }

    private static void AssertNothingRead(FakeJournalSource source)
    {
        Assert.Equal(0, source.ItemCalls);
        Assert.Equal(0, source.MemberCalls);
        Assert.Equal(0, source.SubmissionCalls);
    }

    /// <summary>AC-001: with no <c>courseId</c> only the form is shown and no teaching data is read.</summary>
    [Fact]
    public async Task NoCourseId_ShowsFormOnly_AndReadsNothing()
    {
        var (source, _) = OneCourse();

        var result = await RunAsync(source, new JournalRequest([], [], [], []));

        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        Assert.Null(result.Page.Journal);
        Assert.Null(result.Page.SelectedCourseId);
        Assert.Single(result.Page.Courses);
        AssertNothingRead(source);
    }

    /// <summary>AC-001: the drop-down lists every course, sorted by name in the UI culture, ties by id, with the section.</summary>
    [Fact]
    public async Task Courses_AreSortedByName_TieById_WithSection()
    {
        var source = new FakeJournalSource();
        var yasna = source.AddCourse("Ясна", "9-А");
        var iskra = source.AddCourse("Іскра");
        var sameFirst = source.AddCourse("Астра", "A");
        var sameSecond = source.AddCourse("Астра", "B");

        var result = await RunAsync(source, new JournalRequest([], [], [], []));

        Assert.Equal(
            new[]
            {
                new CourseOption(sameFirst, "Астра", "A"),
                new CourseOption(sameSecond, "Астра", "B"),
                new CourseOption(iskra, "Іскра", null),
                new CourseOption(yasna, "Ясна", "9-А"),
            },
            result.Page.Courses);
    }

    /// <summary>AC-005: <c>view=short</c> and <c>view=SHORT</c> give the short view; absent gives the full view.</summary>
    [Theory]
    [InlineData("short", JournalView.Short)]
    [InlineData("SHORT", JournalView.Short)]
    [InlineData(null, JournalView.Full)]
    public async Task View_IsReadCaseInsensitively_AndDefaultsToFull(string? view, JournalView expected)
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest(
            [Id(course)], [JournalTestData.Period.FromText], [JournalTestData.Period.ToText], view is null ? [] : [view]);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        Assert.Equal(expected, result.Page.View);
    }

    /// <summary>AC-011: a malformed <c>courseId</c> is refused before any data is read.</summary>
    [Theory]
    [MemberData(nameof(MalformedCourseIds))]
    public async Task MalformedCourseId_IsInvalid(string value)
    {
        var (source, _) = OneCourse();
        var request = new JournalRequest([value], [JournalTestData.Period.FromText], [JournalTestData.Period.ToText], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.CourseMalformed }, result.Page.MessageKeys);
        Assert.Null(result.Page.SelectedCourseId);
        AssertNothingRead(source);
    }

    /// <summary>AC-011: a repeated <c>courseId</c> is malformed.</summary>
    [Fact]
    public async Task RepeatedCourseId_IsInvalid()
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest(
            [Id(course), Id(course)], [JournalTestData.Period.FromText], [JournalTestData.Period.ToText], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.CourseMalformed }, result.Page.MessageKeys);
    }

    /// <summary>AC-011: an empty <c>courseId</c> is treated as absent, so the form alone is shown.</summary>
    [Fact]
    public async Task EmptyCourseId_IsTreatedAsAbsent()
    {
        var (source, _) = OneCourse();

        var result = await RunAsync(source, new JournalRequest([""], [], [], []));

        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        Assert.Null(result.Page.Journal);
        Assert.Null(result.Page.SelectedCourseId);
        Assert.Empty(result.Page.MessageKeys);
        AssertNothingRead(source);
    }

    /// <summary>AC-011: a well-formed id of no stored course is unknown and no item is read.</summary>
    [Fact]
    public async Task UnknownCourse_IsCourseUnknown()
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, September(course + 1_000_000));

        Assert.Equal(JournalPageOutcome.CourseUnknown, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.CourseUnknown }, result.Page.MessageKeys);
        Assert.Equal(0, source.ItemCalls);
    }

    /// <summary>AC-011: with no course stored at all an id is still unknown, and the page says nothing is stored.</summary>
    [Fact]
    public async Task UnknownCourse_WhenNoCourseStored_IsCourseUnknown()
    {
        var source = new FakeJournalSource();

        var result = await RunAsync(source, September(42));

        Assert.Equal(JournalPageOutcome.CourseUnknown, result.Outcome);
        Assert.True(result.Page.NoCoursesStored);
        Assert.Equal(new[] { JournalMessageKey.CourseUnknown }, result.Page.MessageKeys);
        Assert.Equal(0, source.ItemCalls);
    }

    /// <summary>AC-011: a malformed <c>from</c> is refused and not kept.</summary>
    [Theory]
    [MemberData(nameof(MalformedDates))]
    public async Task MalformedFrom_IsInvalid(string value)
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest([Id(course)], [value], [JournalTestData.Period.ToText], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.FromMalformed }, result.Page.MessageKeys);
        Assert.Null(result.Page.From);
    }

    /// <summary>AC-011: the bounds 2000-01-01 and 2100-12-31 and a leap day are valid.</summary>
    [Theory]
    [MemberData(nameof(BoundaryDates))]
    public async Task BoundaryDates_AreValid(string value)
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest([Id(course)], [value], [value], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        Assert.Empty(result.Page.MessageKeys);
        Assert.Equal(DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture), result.Page.From);
    }

    /// <summary>AC-011: a malformed <c>to</c> is refused.</summary>
    [Theory]
    [MemberData(nameof(MalformedDates))]
    public async Task MalformedTo_IsInvalid(string value)
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest([Id(course)], [JournalTestData.Period.FromText], [value], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.ToMalformed }, result.Page.MessageKeys);
        Assert.Null(result.Page.To);
    }

    /// <summary>AC-011: a repeated <c>to</c> is malformed.</summary>
    [Fact]
    public async Task RepeatedTo_IsInvalid()
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest(
            [Id(course)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText, JournalTestData.Period.ToText],
            []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.ToMalformed }, result.Page.MessageKeys);
    }

    /// <summary>AC-011: <c>from</c> after <c>to</c> is an inverted period; both dates are kept.</summary>
    [Fact]
    public async Task InvertedPeriod_IsInvalid_AndKeepsBothDates()
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest([Id(course)], ["2026-09-30"], ["2026-09-01"], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.PeriodInverted }, result.Page.MessageKeys);
        Assert.Equal(new DateOnly(2026, 9, 30), result.Page.From);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Page.To);
    }

    /// <summary>AC-011: a one-day period is valid.</summary>
    [Fact]
    public async Task OneDayPeriod_IsValid()
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest([Id(course)], ["2026-09-10"], ["2026-09-10"], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        Assert.Empty(result.Page.MessageKeys);
    }

    /// <summary>AC-011: an unknown <c>view</c> is refused and the view falls back to full.</summary>
    [Fact]
    public async Task UnknownView_IsInvalid_AndViewIsFull()
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest(
            [Id(course)], [JournalTestData.Period.FromText], [JournalTestData.Period.ToText], ["compact"]);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.ViewUnknown }, result.Page.MessageKeys);
        Assert.Equal(JournalView.Full, result.Page.View);
    }

    /// <summary>AC-011: a repeated <c>view</c> is refused and the view falls back to full.</summary>
    [Fact]
    public async Task RepeatedView_IsInvalid_AndViewIsFull()
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest(
            [Id(course)], [JournalTestData.Period.FromText], [JournalTestData.Period.ToText], ["short", "short"]);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.ViewUnknown }, result.Page.MessageKeys);
        Assert.Equal(JournalView.Full, result.Page.View);
    }

    /// <summary>AC-011: all four parameters malformed give exactly the four messages in a fixed order.</summary>
    [Fact]
    public async Task AllFourMalformed_GiveFourMessagesInOrder()
    {
        var (source, _) = OneCourse();

        var result = await RunAsync(source, new JournalRequest(["abc"], ["x"], ["y"], ["z"]));

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(
            new[]
            {
                JournalMessageKey.CourseMalformed,
                JournalMessageKey.FromMalformed,
                JournalMessageKey.ToMalformed,
                JournalMessageKey.ViewUnknown,
            },
            result.Page.MessageKeys);
    }

    /// <summary>AC-011: a malformed date wins over a well-formed unknown course.</summary>
    [Fact]
    public async Task MalformedDate_WithUnknownCourse_IsInvalid_NotCourseUnknown()
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest([Id(course + 1_000_000)], ["2026-9-1"], [JournalTestData.Period.ToText], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.DoesNotContain(JournalMessageKey.CourseUnknown, result.Page.MessageKeys);
    }

    /// <summary>AC-011: a valid course is kept when a date is malformed.</summary>
    [Fact]
    public async Task ValidCourse_IsKept_WhenDateIsMalformed()
    {
        var (source, course) = OneCourse();
        var request = new JournalRequest([Id(course)], ["2026-9-1"], [JournalTestData.Period.ToText], []);

        var result = await RunAsync(source, request);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(course, result.Page.SelectedCourseId);
    }
}
