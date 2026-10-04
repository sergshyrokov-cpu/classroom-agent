using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-025 AC-014 (spec FR-003, FR-009; TC-8): the period is read as whole days of the school's time zone
/// (<c>Europe/Kyiv</c>) and handed to the port as a half-open UTC interval; the default period is the month of B.
/// </summary>
public sealed class JournalPeriodTests
{
    private static GetJournalQuery QueryAt(FakeJournalSource source, DateTimeOffset now) =>
        new(source, new SchoolTimeZone(JournalTestData.Kyiv), new ManualTimeProvider(now));

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static Task<JournalPageResult> RunAsync(
        FakeJournalSource source, long course, string? from, string? to, DateTimeOffset? now = null) =>
        QueryAt(source, now ?? InstallationTestHost.DefaultStart).ExecuteAsync(
            new JournalRequest([Id(course)], from is null ? [] : [from], to is null ? [] : [to], []),
            CultureInfo.GetCultureInfo("uk"),
            TestContext.Current.CancellationToken);

    private static void AssertBounds(FakeJournalSource source, DateTimeOffset start, DateTimeOffset end)
    {
        Assert.NotEmpty(source.Bounds);
        Assert.All(source.Bounds, bound =>
        {
            Assert.Equal(start, bound.Start);
            Assert.Equal(end, bound.EndExclusive);
            Assert.Equal(TimeSpan.Zero, bound.Start.Offset);
            Assert.Equal(TimeSpan.Zero, bound.EndExclusive.Offset);
        });
    }

    private static (FakeJournalSource Source, long Course) CourseWithStudent(params DateTimeOffset[] itemDates)
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        foreach (var date in itemDates)
        {
            source.AddItem(course, date, maxPoints: 10m);
        }

        source.AddMember(course, "Student Test");
        return (source, course);
    }

    /// <summary>AC-014: September in Kyiv (summer time) is the UTC interval 31 Aug 21:00 to 30 Sep 21:00, in UTC offsets.</summary>
    [Fact]
    public async Task September_ReadsSummerTimeBounds()
    {
        var (source, course) = CourseWithStudent(JournalTestData.Period.Early);

        await RunAsync(source, course, JournalTestData.Period.FromText, JournalTestData.Period.ToText);

        AssertBounds(source, JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc);
    }

    /// <summary>AC-014: an item at 00:30 on 1 Oct Kyiv time is outside September.</summary>
    [Fact]
    public async Task ItemJustAfterPeriodEnd_IsNotAColumn()
    {
        var after = new DateTimeOffset(2026, 9, 30, 21, 30, 0, TimeSpan.Zero);
        var (source, course) = CourseWithStudent(JournalTestData.Period.Early, after);

        var result = await RunAsync(source, course, JournalTestData.Period.FromText, JournalTestData.Period.ToText);

        var journalOrNull = result.Page.Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;
        Assert.Single(journal.Columns);
    }

    /// <summary>AC-014: an item at 00:30 on 1 Sep Kyiv time is inside September.</summary>
    [Fact]
    public async Task ItemJustAfterPeriodStart_IsAColumn()
    {
        var atStart = new DateTimeOffset(2026, 8, 31, 21, 30, 0, TimeSpan.Zero);
        var (source, course) = CourseWithStudent(atStart);

        var result = await RunAsync(source, course, JournalTestData.Period.FromText, JournalTestData.Period.ToText);

        var journalOrNull = result.Page.Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;
        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(journal.Columns).Date);
    }

    /// <summary>AC-014: December in Kyiv (winter time) is the UTC interval 30 Nov 22:00 to 31 Dec 22:00.</summary>
    [Fact]
    public async Task December_ReadsWinterTimeBounds()
    {
        var (source, course) = CourseWithStudent();

        await RunAsync(source, course, "2026-12-01", "2026-12-31");

        AssertBounds(
            source,
            new DateTimeOffset(2026, 11, 30, 22, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 22, 0, 0, TimeSpan.Zero));
    }

    /// <summary>AC-014: 25 Oct 2026, the day summer time ends, is a 25-hour day.</summary>
    [Fact]
    public async Task DaylightSavingEndDay_IsTwentyFiveHours()
    {
        var (source, course) = CourseWithStudent();

        await RunAsync(source, course, "2026-10-25", "2026-10-25");

        AssertBounds(
            source,
            new DateTimeOffset(2026, 10, 24, 21, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 25, 22, 0, 0, TimeSpan.Zero));
    }

    /// <summary>AC-014: without dates the period is the month of B in the school's zone.</summary>
    [Fact]
    public async Task DefaultPeriod_IsTheMonthOfB()
    {
        var (source, course) = CourseWithStudent();

        var result = await RunAsync(source, course, null, null);

        Assert.Equal(new DateOnly(2026, 9, 1), result.Page.From);
        Assert.Equal(new DateOnly(2026, 9, 30), result.Page.To);
    }

    /// <summary>AC-014: B at 22:30 UTC on 30 Sep is already 1 Oct in Kyiv, so the default is October.</summary>
    [Fact]
    public async Task DefaultPeriod_FollowsTheSchoolDate_NotTheUtcDate()
    {
        var (source, course) = CourseWithStudent();
        var now = new DateTimeOffset(2026, 9, 30, 22, 30, 0, TimeSpan.Zero);

        var result = await RunAsync(source, course, null, null, now);

        Assert.Equal(new DateOnly(2026, 10, 1), result.Page.From);
        Assert.Equal(new DateOnly(2026, 10, 31), result.Page.To);
    }

    /// <summary>AC-014: only <c>from</c> after the default <c>to</c> gives an inverted period.</summary>
    [Fact]
    public async Task OnlyFrom_AfterDefaultTo_IsPeriodInverted()
    {
        var (source, course) = CourseWithStudent();

        var result = await RunAsync(source, course, "2026-10-05", null);

        Assert.Equal(JournalPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { JournalMessageKey.PeriodInverted }, result.Page.MessageKeys);
    }
}
