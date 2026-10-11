using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-033 AC-001, AC-013 (spec FR-002, FR-003, FR-013, VR-001, VR-002, §6): the reports request — the form without a
/// course, the default period filled for each missing date on its own, and every way a parameter can be refused, with
/// the message keys and no meeting data read once the request is refused.
/// </summary>
public sealed class MeetReportsRequestValidationTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk");

    public static TheoryData<string> MalformedCourseIds => new("abc", "-1", "0", "1 ", "9223372036854775808");

    public static TheoryData<string> MalformedDates =>
        new("2026-13-01", "2026-02-30", "1999-12-31", "2101-01-01", "01.10.2026");

    public static TheoryData<string> BoundaryDates => new("2000-01-01", "2100-12-31");

    private static GetMeetReportsQuery QueryOver(FakeMeetReportSource source) =>
        new(
            source.Courses,
            source,
            new GetLastSynchronizationQuery(new SyncWorld.StateRepository(), new SchoolTimeZone(MeetReportsTestData.Kyiv)),
            new SchoolTimeZone(MeetReportsTestData.Kyiv),
            new ManualTimeProvider(MeetReportsTestData.Now));

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static Task<MeetReportsPageResult> RunAsync(FakeMeetReportSource source, MeetReportsRequest request) =>
        QueryOver(source).ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);

    private static (FakeMeetReportSource Source, long Course) OneCourse()
    {
        var source = new FakeMeetReportSource();
        return (source, source.AddCourse("Course"));
    }

    private static void AssertNothingRead(FakeMeetReportSource source)
    {
        Assert.Empty(source.Bounds);
        Assert.Empty(source.PeopleRequests);
    }

    /// <summary>AC-001: with no course the form is shown with the October default period and no reports.</summary>
    [Fact]
    public async Task NoCourseId_ShowsFormOnly_WithTheDefaultPeriod()
    {
        var (source, _) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([], [], []));

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.Null(result.Page.Reports);
        Assert.Null(result.Page.SelectedCourseId);
        Assert.Equal(MeetReportsTestData.Period.From, result.Page.From);
        Assert.Equal(MeetReportsTestData.Period.To, result.Page.To);
        Assert.Single(result.Page.Courses);
        Assert.False(result.Page.NoCoursesStored);
        AssertNothingRead(source);
    }

    /// <summary>AC-001: every stored course is in the drop-down.</summary>
    [Fact]
    public async Task TheDropDown_ListsEveryStoredCourse()
    {
        var source = new FakeMeetReportSource();
        source.AddCourse("Alpha");
        source.AddCourse("Beta");
        source.AddCourse("Gamma");

        var result = await RunAsync(source, new MeetReportsRequest([], [], []));

        Assert.Equal(
            new[] { "Alpha", "Beta", "Gamma" },
            result.Page.Courses.Select(c => c.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>AC-001: only <c>from</c> given — <c>to</c> is the last day of the current month on its own.</summary>
    [Fact]
    public async Task OnlyFrom_FillsToWithTheLastDayOfTheCurrentMonth()
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([Id(course)], ["2026-10-10"], []));

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.Equal(new DateOnly(2026, 10, 10), result.Page.From);
        Assert.Equal(new DateOnly(2026, 10, 31), result.Page.To);
    }

    /// <summary>AC-001: only <c>to</c> given — <c>from</c> is the first day of the current month on its own.</summary>
    [Fact]
    public async Task OnlyTo_FillsFromWithTheFirstDayOfTheCurrentMonth()
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([Id(course)], [], ["2026-10-20"]));

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.Equal(new DateOnly(2026, 10, 1), result.Page.From);
        Assert.Equal(new DateOnly(2026, 10, 20), result.Page.To);
    }

    /// <summary>AC-001: no course stored — the page says so, and there are no reports.</summary>
    [Fact]
    public async Task WithNoCourseStored_TheFlagIsSet()
    {
        var source = new FakeMeetReportSource();

        var result = await RunAsync(source, new MeetReportsRequest([], [], []));

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.True(result.Page.NoCoursesStored);
        Assert.Empty(result.Page.Courses);
        Assert.Null(result.Page.Reports);
    }

    /// <summary>AC-001: a valid course and period show the form values and the reports.</summary>
    [Fact]
    public async Task AValidRequest_ShowsTheChosenValues_AndReports()
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(
            source,
            new MeetReportsRequest([Id(course)], [MeetReportsTestData.Period.FromText], [MeetReportsTestData.Period.ToText]));

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.Equal(course, result.Page.SelectedCourseId);
        Assert.Equal(MeetReportsTestData.Period.From, result.Page.From);
        Assert.Equal(MeetReportsTestData.Period.To, result.Page.To);
        Assert.Empty(result.Page.MessageKeys);
        Assert.NotNull(result.Page.Reports);
    }

    /// <summary>AC-013: an empty <c>courseId</c> is treated as absent, so the form alone is shown.</summary>
    [Fact]
    public async Task EmptyCourseId_IsTreatedAsAbsent()
    {
        var (source, _) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([""], [], []));

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.Null(result.Page.Reports);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: a malformed <c>courseId</c> is refused before any meeting data is read.</summary>
    [Theory]
    [MemberData(nameof(MalformedCourseIds))]
    public async Task MalformedCourseId_IsInvalid(string value)
    {
        var (source, _) = OneCourse();
        var request = new MeetReportsRequest([value], [MeetReportsTestData.Period.FromText], [MeetReportsTestData.Period.ToText]);

        var result = await RunAsync(source, request);

        Assert.Equal(MeetReportsPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.CourseMalformed }, result.Page.MessageKeys);
        Assert.Null(result.Page.SelectedCourseId);
        Assert.Null(result.Page.Reports);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: a repeated <c>courseId</c> is malformed.</summary>
    [Fact]
    public async Task RepeatedCourseId_IsInvalid()
    {
        var (source, course) = OneCourse();
        var request = new MeetReportsRequest(
            [Id(course), Id(course)], [MeetReportsTestData.Period.FromText], [MeetReportsTestData.Period.ToText]);

        var result = await RunAsync(source, request);

        Assert.Equal(MeetReportsPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.CourseMalformed }, result.Page.MessageKeys);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: a well-formed id of no stored course is unknown (404), and no meeting data is read.</summary>
    [Fact]
    public async Task UnknownCourse_IsReportedAsUnknown()
    {
        var (source, course) = OneCourse();
        var request = new MeetReportsRequest(
            [Id(course + 1000)], [MeetReportsTestData.Period.FromText], [MeetReportsTestData.Period.ToText]);

        var result = await RunAsync(source, request);

        Assert.Equal(MeetReportsPageOutcome.CourseUnknown, result.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.CourseUnknown }, result.Page.MessageKeys);
        Assert.Null(result.Page.Reports);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: a malformed <c>from</c> is refused.</summary>
    [Theory]
    [MemberData(nameof(MalformedDates))]
    public async Task MalformedFrom_IsInvalid(string value)
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([Id(course)], [value], [MeetReportsTestData.Period.ToText]));

        Assert.Equal(MeetReportsPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.FromMalformed }, result.Page.MessageKeys);
        Assert.Null(result.Page.Reports);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: a malformed <c>to</c> is refused.</summary>
    [Theory]
    [MemberData(nameof(MalformedDates))]
    public async Task MalformedTo_IsInvalid(string value)
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([Id(course)], [MeetReportsTestData.Period.FromText], [value]));

        Assert.Equal(MeetReportsPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.ToMalformed }, result.Page.MessageKeys);
        Assert.Null(result.Page.Reports);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: a repeated <c>from</c> or <c>to</c> is malformed.</summary>
    [Fact]
    public async Task RepeatedDates_AreMalformed()
    {
        var (source, course) = OneCourse();

        var repeatedFrom = await RunAsync(
            source,
            new MeetReportsRequest(
                [Id(course)],
                [MeetReportsTestData.Period.FromText, MeetReportsTestData.Period.FromText],
                [MeetReportsTestData.Period.ToText]));
        var repeatedTo = await RunAsync(
            source,
            new MeetReportsRequest(
                [Id(course)],
                [MeetReportsTestData.Period.FromText],
                [MeetReportsTestData.Period.ToText, MeetReportsTestData.Period.ToText]));

        Assert.Equal(MeetReportsPageOutcome.Invalid, repeatedFrom.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.FromMalformed }, repeatedFrom.Page.MessageKeys);
        Assert.Equal(MeetReportsPageOutcome.Invalid, repeatedTo.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.ToMalformed }, repeatedTo.Page.MessageKeys);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: an inverted period is refused.</summary>
    [Fact]
    public async Task AnInvertedPeriod_IsInvalid()
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([Id(course)], ["2026-10-20"], ["2026-10-10"]));

        Assert.Equal(MeetReportsPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.PeriodInverted }, result.Page.MessageKeys);
        Assert.Null(result.Page.Reports);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: only <c>from</c> after the filled default <c>to</c> is an inverted period.</summary>
    [Fact]
    public async Task AFromAfterTheDefaultTo_IsInverted()
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([Id(course)], ["2026-11-05"], []));

        Assert.Equal(MeetReportsPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { MeetReportsMessageKey.PeriodInverted }, result.Page.MessageKeys);
        AssertNothingRead(source);
    }

    /// <summary>AC-013: a one-day period is valid.</summary>
    [Fact]
    public async Task AOneDayPeriod_IsValid()
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([Id(course)], ["2026-10-10"], ["2026-10-10"]));

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.Empty(result.Page.MessageKeys);
        Assert.NotNull(result.Page.Reports);
    }

    /// <summary>AC-013: years 2000 and 2100 are the inclusive limits.</summary>
    [Theory]
    [MemberData(nameof(BoundaryDates))]
    public async Task BoundaryYears_AreValid(string value)
    {
        var (source, course) = OneCourse();

        var result = await RunAsync(source, new MeetReportsRequest([Id(course)], [value], [value]));

        Assert.Equal(MeetReportsPageOutcome.Shown, result.Outcome);
        Assert.Empty(result.Page.MessageKeys);
        Assert.NotNull(result.Page.Reports);
    }
}
