using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-025 AC-001, AC-007, AC-012, AC-013 (spec FR-002, FR-003, FR-008, FR-009, FR-012): the columns of the journal —
/// their count and order, their header, the kind of each item, the empty states, and the bounded reads of the port.
/// </summary>
public sealed class JournalColumnsTests
{
    private static GetJournalQuery QueryOver(FakeJournalSource source) =>
        new(source, new SchoolTimeZone(JournalTestData.Kyiv), new ManualTimeProvider(InstallationTestHost.DefaultStart));

    private static JournalRequest Request(long courseId) =>
        new(
            [courseId.ToString(CultureInfo.InvariantCulture)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText],
            []);

    private static async Task<JournalPageModel> PageAsync(FakeJournalSource source, long courseId, string culture = "uk")
    {
        var result = await QueryOver(source).ExecuteAsync(
            Request(courseId), CultureInfo.GetCultureInfo(culture), TestContext.Current.CancellationToken);

        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        return result.Page;
    }

    /// <summary>AC-001: 3 items by 2 students give 3 columns and 2 rows of 3 cells in column order.</summary>
    [Fact]
    public async Task ThreeItemsTwoStudents_GiveThreeColumns_TwoRows_CellsInColumnOrder()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        var third = source.AddItem(course, JournalTestData.Period.Early.AddDays(2), title: "Third", maxPoints: 10m);
        var first = source.AddItem(course, JournalTestData.Period.Early, title: "First", maxPoints: 10m);
        var second = source.AddItem(course, JournalTestData.Period.Early.AddDays(1), title: "Second", maxPoints: 10m);
        foreach (var name in new[] { "Alpha Test", "Bravo Test" })
        {
            var member = source.AddMember(course, name);
            source.AddSubmission(first, member, SubmissionState.TurnedIn, assignedGrade: 1m);
            source.AddSubmission(second, member, SubmissionState.TurnedIn, assignedGrade: 2m);
            source.AddSubmission(third, member, SubmissionState.TurnedIn, assignedGrade: 3m);
        }

        var journalOrNull = (await PageAsync(source, course)).Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;

        Assert.Equal(3, journal.Columns.Count);
        Assert.Equal(["First", "Second", "Third"], journal.Columns.Select(c => c.Title));
        Assert.Equal(2, journal.Rows.Count);
        Assert.All(journal.Rows, row =>
        {
            Assert.Equal(3, row.Cells.Count);
            Assert.Equal([1m, 2m, 3m], row.Cells.Select(c => c.Points));
        });
    }

    /// <summary>AC-001: the header carries the title as given, the Kyiv date, the kind and the maximum of graded work.</summary>
    [Fact]
    public async Task Header_TitleDateKindAndMaxPoints()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        var itemDate = new DateTimeOffset(2026, 9, 5, 22, 30, 0, TimeSpan.Zero);
        source.AddItem(course, itemDate, title: "Graded <b>work</b>", maxPoints: 12m);
        source.AddMember(course, "Student Test");

        var journalOrNull = (await PageAsync(source, course)).Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;

        var column = Assert.Single(journal.Columns);
        Assert.Equal("Graded <b>work</b>", column.Title);
        Assert.Equal(new DateOnly(2026, 9, 6), column.Date);
        Assert.Equal(CourseWorkKind.GradedWork, column.Kind);
        Assert.Equal(12m, column.MaxPoints);
    }

    /// <summary>AC-001, AC-007: ungraded work and a material carry no maximum, and each has its own kind.</summary>
    [Fact]
    public async Task Kinds_PerItem_AndMaxPointsOnlyForGraded()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        source.AddItem(course, JournalTestData.Period.Early, title: "A graded", maxPoints: 10m);
        source.AddItem(course, JournalTestData.Period.Early.AddDays(1), title: "B ungraded");
        source.AddItem(course, JournalTestData.Period.Early.AddDays(2), title: "C material", material: true);
        source.AddMember(course, "Student Test");

        var journalOrNull = (await PageAsync(source, course)).Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;

        Assert.Equal(
            [CourseWorkKind.GradedWork, CourseWorkKind.UngradedWork, CourseWorkKind.Material],
            journal.Columns.Select(c => c.Kind));
        Assert.Equal([10m, null, null], journal.Columns.Select(c => c.MaxPoints));
    }

    /// <summary>AC-001: items of the same instant are ordered by the title in the UI culture.</summary>
    [Fact]
    public async Task Order_SameInstant_ByTitleInUiCulture()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        source.AddItem(course, JournalTestData.Period.Early, title: "Ярлик");
        source.AddItem(course, JournalTestData.Period.Early, title: "Іменник");
        source.AddItem(course, JournalTestData.Period.Early, title: "Абетка");
        source.AddMember(course, "Student Test");

        var journalOrNull = (await PageAsync(source, course, "uk")).Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;

        Assert.Equal(["Абетка", "Іменник", "Ярлик"], journal.Columns.Select(c => c.Title));
    }

    /// <summary>AC-001: items of the same instant and title are ordered by id, the lower first.</summary>
    [Fact]
    public async Task Order_SameInstantAndTitle_ByLowerIdFirst()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        source.AddItem(course, JournalTestData.Period.Early, title: "Same", maxPoints: 5m);
        source.AddItem(course, JournalTestData.Period.Early, title: "Same", maxPoints: 7m);
        source.AddMember(course, "Student Test");

        var journalOrNull = (await PageAsync(source, course)).Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;

        Assert.Equal([5m, 7m], journal.Columns.Select(c => c.MaxPoints));
    }

    /// <summary>AC-012: a course with no item in the period has the "no columns" empty state and no journal.</summary>
    [Fact]
    public async Task NoItems_GivesNoColumnsEmptyState_AndReadsNeitherMembersNorSubmissions()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        source.AddMember(course, "Student Test");

        var page = await PageAsync(source, course);

        Assert.Equal(JournalEmptyStateKey.NoColumns, page.EmptyStateKey);
        Assert.Null(page.Journal);
        Assert.Equal(0, source.MemberCalls);
        Assert.Equal(0, source.SubmissionCalls);
    }

    /// <summary>AC-012: items but no student give the "no rows" empty state and no journal.</summary>
    [Fact]
    public async Task ItemsButNoStudents_GivesNoRowsEmptyState_AndReadsNoSubmissions()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        source.AddItem(course, JournalTestData.Period.Early, maxPoints: 10m);

        var page = await PageAsync(source, course);

        Assert.Equal(JournalEmptyStateKey.NoRows, page.EmptyStateKey);
        Assert.Null(page.Journal);
        Assert.Equal(0, source.SubmissionCalls);
    }

    /// <summary>AC-013: a 1 by 1 journal calls each port method exactly once.</summary>
    [Fact]
    public async Task OneByOne_CallsEachPortMethodOnce()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        var item = source.AddItem(course, JournalTestData.Period.Early, maxPoints: 10m);
        var member = source.AddMember(course, "Student Test");
        source.AddSubmission(item, member, SubmissionState.TurnedIn, assignedGrade: 5m);

        var journalOrNull = (await PageAsync(source, course)).Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;

        Assert.Single(Assert.Single(journal.Rows).Cells);
        Assert.Equal(1, source.ItemCalls);
        Assert.Equal(1, source.MemberCalls);
        Assert.Equal(1, source.SubmissionCalls);
    }

    /// <summary>AC-013: a 5 by 4 journal still calls each port method at most once.</summary>
    [Fact]
    public async Task FiveByFour_CallsEachPortMethodAtMostOnce()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        var items = Enumerable.Range(0, 5)
            .Select(n => source.AddItem(course, JournalTestData.Period.Early.AddDays(n), maxPoints: 10m))
            .ToList();
        for (var m = 0; m < 4; m++)
        {
            var member = source.AddMember(course, $"Student {m} Test");
            foreach (var item in items)
            {
                source.AddSubmission(item, member, SubmissionState.TurnedIn, assignedGrade: 5m);
            }
        }

        var journalOrNull = (await PageAsync(source, course)).Journal;

        Assert.NotNull(journalOrNull);

        var journal = journalOrNull!;

        Assert.Equal(5, journal.Columns.Count);
        Assert.Equal(4, journal.Rows.Count);
        Assert.InRange(source.ItemCalls, 1, 1);
        Assert.InRange(source.MemberCalls, 1, 1);
        Assert.InRange(source.SubmissionCalls, 1, 1);
    }
}
