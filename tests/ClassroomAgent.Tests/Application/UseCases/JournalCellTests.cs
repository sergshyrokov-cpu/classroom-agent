using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-025 AC-003 … AC-007 (spec FR-004, FR-005, FR-006, FR-007, I-1 … I-3, OD-008): the state of one journal cell —
/// grade, turned-in, returned, not turned in, not due yet, not assigned, unrecognised, ungraded and material — the
/// late mark, the draft grade and the turn-in date. B is <see cref="InstallationTestHost.DefaultStart"/>.
/// </summary>
public sealed class JournalCellTests
{
    private static readonly DateTimeOffset B = InstallationTestHost.DefaultStart;

    public static TheoryData<SubmissionState> RecognisedStates => new(
        SubmissionState.New,
        SubmissionState.Created,
        SubmissionState.TurnedIn,
        SubmissionState.Returned,
        SubmissionState.ReclaimedByStudent,
        SubmissionState.StudentEditedAfterTurnIn);

    public static TheoryData<SubmissionState> NotTurnedInStates => new(
        SubmissionState.New,
        SubmissionState.Created,
        SubmissionState.ReclaimedByStudent);

    /// <summary>Builds one course with one item and one student and returns the student's only cell.</summary>
    private static async Task<JournalCell> CellAsync(
        Action<FakeJournalSource, long, long>? submit,
        decimal? maxPoints = 10m,
        DateTimeOffset? dueAt = null,
        bool material = false,
        string view = "full")
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        var item = source.AddItem(course, JournalTestData.Period.Early, maxPoints: maxPoints, dueAt: dueAt, material: material);
        var member = source.AddMember(course, "Student Test");
        submit?.Invoke(source, item, member);

        var result = await QueryOver(source).ExecuteAsync(Request(course, view), CultureInfo.GetCultureInfo("uk"), ct);

        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        var journalOrNull = result.Page.Journal;
        Assert.NotNull(journalOrNull);
        var journal = journalOrNull!;
        return Assert.Single(Assert.Single(journal.Rows).Cells);
    }

    private static GetJournalQuery QueryOver(FakeJournalSource source) =>
        new(source, new SchoolTimeZone(JournalTestData.Kyiv), new ManualTimeProvider(B));

    private static JournalRequest Request(long courseId, string view) =>
        new(
            [courseId.ToString(CultureInfo.InvariantCulture)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText],
            view == "full" ? [] : [view]);

    /// <summary>AC-003: a graded submission with an assigned grade shows the grade out of the maximum.</summary>
    [Fact]
    public async Task AssignedGrade_OnTurnedIn_IsGradeWithPointsAndMaximum()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 8.5m));

        Assert.Equal(JournalCellState.Grade, cell.State);
        Assert.Equal(8.5m, cell.Points);
        Assert.Equal(10m, cell.MaxPoints);
    }

    /// <summary>AC-003: the assigned grade wins in every recognised state.</summary>
    [Theory]
    [MemberData(nameof(RecognisedStates))]
    public async Task AssignedGrade_WinsInEveryRecognisedState(SubmissionState state)
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, state, assignedGrade: 7m));

        Assert.Equal(JournalCellState.Grade, cell.State);
        Assert.Equal(7m, cell.Points);
    }

    /// <summary>AC-003: a grade of zero is a grade, never an empty cell.</summary>
    [Fact]
    public async Task ZeroGrade_IsGradeWithZeroPoints()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 0m));

        Assert.Equal(JournalCellState.Grade, cell.State);
        Assert.Equal(0m, cell.Points);
    }

    /// <summary>AC-003: turned in without a grade.</summary>
    [Theory]
    [InlineData(SubmissionState.TurnedIn)]
    [InlineData(SubmissionState.StudentEditedAfterTurnIn)]
    public async Task TurnedIn_WithoutGrade_IsTurnedInNotGraded(SubmissionState state)
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, state));

        Assert.Equal(JournalCellState.TurnedInNotGraded, cell.State);
        Assert.Null(cell.Points);
    }

    /// <summary>AC-003: returned without a grade.</summary>
    [Fact]
    public async Task Returned_WithoutGrade_IsReturnedWithoutGrade()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.Returned));

        Assert.Equal(JournalCellState.ReturnedWithoutGrade, cell.State);
    }

    /// <summary>AC-003: not turned in and no due date.</summary>
    [Theory]
    [MemberData(nameof(NotTurnedInStates))]
    public async Task NotTurnedIn_WithoutDueDate_IsNotTurnedInNoDueDate(SubmissionState state)
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, state));

        Assert.Equal(JournalCellState.NotTurnedInNoDueDate, cell.State);
    }

    /// <summary>AC-003: not turned in and the due date is before B.</summary>
    [Theory]
    [MemberData(nameof(NotTurnedInStates))]
    public async Task NotTurnedIn_DueBeforeB_IsNotTurnedIn(SubmissionState state)
    {
        var cell = await CellAsync(
            (s, i, m) => s.AddSubmission(i, m, state), dueAt: JournalTestData.Period.Early);

        Assert.Equal(JournalCellState.NotTurnedIn, cell.State);
    }

    /// <summary>AC-003: not turned in and the due date is after B, or exactly B (strict <c>DueAt &lt; B</c>).</summary>
    [Theory]
    [MemberData(nameof(NotTurnedInStates))]
    public async Task NotTurnedIn_DueAfterB_IsNotDueYet(SubmissionState state)
    {
        var cell = await CellAsync(
            (s, i, m) => s.AddSubmission(i, m, state), dueAt: JournalTestData.Period.Late);

        Assert.Equal(JournalCellState.NotDueYet, cell.State);
    }

    /// <summary>AC-003: a due date equal to B is not yet overdue.</summary>
    [Theory]
    [MemberData(nameof(NotTurnedInStates))]
    public async Task NotTurnedIn_DueExactlyB_IsNotDueYet(SubmissionState state)
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, state), dueAt: B);

        Assert.Equal(JournalCellState.NotDueYet, cell.State);
    }

    /// <summary>AC-003: the late mark rides on a grade without changing the state.</summary>
    [Fact]
    public async Task Late_OnGrade_KeepsStateGrade()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 9m, late: true));

        Assert.Equal(JournalCellState.Grade, cell.State);
        Assert.True(cell.Late);
    }

    /// <summary>AC-003: the late mark rides on a not-turned-in cell without changing the state.</summary>
    [Fact]
    public async Task Late_OnNotTurnedIn_KeepsStateNotTurnedIn()
    {
        var cell = await CellAsync(
            (s, i, m) => s.AddSubmission(i, m, SubmissionState.New, late: true), dueAt: JournalTestData.Period.Early);

        Assert.Equal(JournalCellState.NotTurnedIn, cell.State);
        Assert.True(cell.Late);
    }

    /// <summary>AC-003: the late mark rides on a turned-in-not-graded cell without changing the state.</summary>
    [Fact]
    public async Task Late_OnTurnedInNotGraded_KeepsState()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, late: true));

        Assert.Equal(JournalCellState.TurnedInNotGraded, cell.State);
        Assert.True(cell.Late);
    }

    /// <summary>AC-003: without the late flag the mark is off.</summary>
    [Fact]
    public async Task NotLate_IsFalse()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 5m, late: false));

        Assert.Equal(JournalCellState.Grade, cell.State);
        Assert.False(cell.Late);
    }

    /// <summary>AC-003 / OD-008 a: of two submissions the newer <c>UpdateTime</c> wins.</summary>
    [Fact]
    public async Task Duplicates_NewerUpdateTimeWins()
    {
        var cell = await CellAsync((s, i, m) =>
        {
            s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 3m, updateTime: JournalTestData.Period.Early, id: 5000);
            s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 4m, updateTime: JournalTestData.Period.Late, id: 4000);
        });

        Assert.Equal(JournalCellState.Grade, cell.State);
        Assert.Equal(4m, cell.Points);
    }

    /// <summary>AC-003 / OD-008 a: an absent <c>UpdateTime</c> is older than any present one.</summary>
    [Fact]
    public async Task Duplicates_AbsentUpdateTime_IsOlderThanAny()
    {
        var cell = await CellAsync((s, i, m) =>
        {
            s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 3m, updateTime: null, id: 5000);
            s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 4m, updateTime: JournalTestData.Period.Early, id: 4000);
        });

        Assert.Equal(4m, cell.Points);
    }

    /// <summary>AC-003 / OD-008 a: equal <c>UpdateTime</c> falls back to the larger id.</summary>
    [Fact]
    public async Task Duplicates_EqualUpdateTime_LargerIdWins()
    {
        var cell = await CellAsync((s, i, m) =>
        {
            s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 3m, updateTime: JournalTestData.Period.Early, id: 4000);
            s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 4m, updateTime: JournalTestData.Period.Early, id: 5000);
        });

        Assert.Equal(4m, cell.Points);
    }

    /// <summary>AC-003 / OD-008 a: both <c>UpdateTime</c> absent falls back to the larger id.</summary>
    [Fact]
    public async Task Duplicates_BothUpdateTimeAbsent_LargerIdWins()
    {
        var cell = await CellAsync((s, i, m) =>
        {
            s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 4m, updateTime: null, id: 5000);
            s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 3m, updateTime: null, id: 4000);
        });

        Assert.Equal(4m, cell.Points);
    }

    /// <summary>AC-004: graded work with no submission is not assigned.</summary>
    [Fact]
    public async Task Graded_WithoutSubmission_IsNotAssigned()
    {
        var cell = await CellAsync(null);

        Assert.Equal(JournalCellState.NotAssigned, cell.State);
        Assert.False(cell.Late);
        Assert.Null(cell.Points);
        Assert.Null(cell.DraftPoints);
    }

    /// <summary>AC-004: ungraded work with no submission is not assigned.</summary>
    [Fact]
    public async Task Ungraded_WithoutSubmission_IsNotAssigned()
    {
        var cell = await CellAsync(null, maxPoints: null);

        Assert.Equal(JournalCellState.NotAssigned, cell.State);
        Assert.False(cell.Late);
        Assert.Null(cell.Points);
        Assert.Null(cell.DraftPoints);
    }

    /// <summary>AC-005: the full view shows the draft grade of a turned-in, ungraded-by-teacher submission.</summary>
    [Fact]
    public async Task FullView_ShowsDraftGrade()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, draftGrade: 6.25m));

        Assert.Equal(JournalCellState.TurnedInNotGraded, cell.State);
        Assert.Equal(6.25m, cell.DraftPoints);
    }

    /// <summary>AC-005: the short view hides the draft grade.</summary>
    [Fact]
    public async Task ShortView_HidesDraftGrade()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, draftGrade: 6.25m), view: "short");

        Assert.Equal(JournalCellState.TurnedInNotGraded, cell.State);
        Assert.Null(cell.DraftPoints);
    }

    /// <summary>AC-005 / I-2: with an assigned grade the draft grade is not shown.</summary>
    [Fact]
    public async Task AssignedGrade_SuppressesDraftGrade()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 8m, draftGrade: 6.25m));

        Assert.Equal(JournalCellState.Grade, cell.State);
        Assert.Null(cell.DraftPoints);
    }

    /// <summary>AC-005: ungraded work never shows a draft grade.</summary>
    [Fact]
    public async Task UngradedWork_HasNoDraftGrade()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, draftGrade: 6.25m), maxPoints: null);

        Assert.Equal(JournalCellState.TurnedIn, cell.State);
        Assert.Null(cell.DraftPoints);
    }

    /// <summary>AC-005: the full view carries the turn-in date in the school's zone.</summary>
    [Fact]
    public async Task FullView_TurnedInOn_IsKyivDate()
    {
        var turnedIn = new DateTimeOffset(2026, 9, 5, 22, 30, 0, TimeSpan.Zero);

        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, turnedInAt: turnedIn));

        Assert.Equal(new DateOnly(2026, 9, 6), cell.TurnedInOn);
    }

    /// <summary>AC-005: the short view carries no turn-in date.</summary>
    [Fact]
    public async Task ShortView_TurnedInOn_IsNull()
    {
        var turnedIn = new DateTimeOffset(2026, 9, 5, 22, 30, 0, TimeSpan.Zero);

        var cell = await CellAsync(
            (s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, turnedInAt: turnedIn), view: "short");

        Assert.Null(cell.TurnedInOn);
        Assert.Equal(JournalCellState.TurnedInNotGraded, cell.State);
    }

    /// <summary>AC-006: an unrecognised state shows its raw text and no points at all.</summary>
    [Fact]
    public async Task Unrecognised_ShowsRawState_AndNoPoints()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(
            i, m, SubmissionState.Unrecognised, CourseWorkTestData.UnrecognisedState, assignedGrade: 7m, draftGrade: 6m));

        Assert.Equal(JournalCellState.Unrecognised, cell.State);
        Assert.Equal(CourseWorkTestData.UnrecognisedState, cell.RawState);
        Assert.Null(cell.Points);
        Assert.Null(cell.DraftPoints);
    }

    /// <summary>AC-006 / I-3: the late mark is kept on an unrecognised state.</summary>
    [Fact]
    public async Task Unrecognised_KeepsLateMark()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(
            i, m, SubmissionState.Unrecognised, CourseWorkTestData.UnrecognisedState, late: true));

        Assert.Equal(JournalCellState.Unrecognised, cell.State);
        Assert.True(cell.Late);
    }

    /// <summary>AC-006: an unrecognised state past its due date is never "not turned in".</summary>
    [Fact]
    public async Task Unrecognised_DuePassed_IsNeverNotTurnedIn()
    {
        var cell = await CellAsync(
            (s, i, m) => s.AddSubmission(i, m, SubmissionState.Unrecognised, CourseWorkTestData.UnrecognisedState),
            dueAt: JournalTestData.Period.Early);

        Assert.Equal(JournalCellState.Unrecognised, cell.State);
    }

    /// <summary>AC-007: ungraded work turned in is plain "turned in".</summary>
    [Theory]
    [InlineData(SubmissionState.TurnedIn)]
    [InlineData(SubmissionState.StudentEditedAfterTurnIn)]
    public async Task Ungraded_TurnedIn_IsTurnedIn(SubmissionState state)
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, state), maxPoints: null);

        Assert.Equal(JournalCellState.TurnedIn, cell.State);
    }

    /// <summary>AC-007: ungraded work returned is plain "returned".</summary>
    [Fact]
    public async Task Ungraded_Returned_IsReturned()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.Returned), maxPoints: null);

        Assert.Equal(JournalCellState.Returned, cell.State);
    }

    /// <summary>AC-007: a grade Google sent for ungraded work is not shown.</summary>
    [Fact]
    public async Task Ungraded_WithAssignedGrade_StaysTurnedIn_WithoutPoints()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 5m), maxPoints: null);

        Assert.Equal(JournalCellState.TurnedIn, cell.State);
        Assert.Null(cell.Points);
    }

    /// <summary>AC-007: ungraded work without due date, not turned in.</summary>
    [Fact]
    public async Task Ungraded_NotTurnedIn_WithoutDueDate()
    {
        var cell = await CellAsync((s, i, m) => s.AddSubmission(i, m, SubmissionState.New), maxPoints: null);

        Assert.Equal(JournalCellState.NotTurnedInNoDueDate, cell.State);
    }

    /// <summary>AC-007: ungraded work past its due date, not turned in.</summary>
    [Fact]
    public async Task Ungraded_NotTurnedIn_DuePassed()
    {
        var cell = await CellAsync(
            (s, i, m) => s.AddSubmission(i, m, SubmissionState.New), maxPoints: null, dueAt: JournalTestData.Period.Early);

        Assert.Equal(JournalCellState.NotTurnedIn, cell.State);
    }

    /// <summary>AC-007: ungraded work due later, not turned in.</summary>
    [Fact]
    public async Task Ungraded_NotTurnedIn_DueLater()
    {
        var cell = await CellAsync(
            (s, i, m) => s.AddSubmission(i, m, SubmissionState.New), maxPoints: null, dueAt: JournalTestData.Period.Late);

        Assert.Equal(JournalCellState.NotDueYet, cell.State);
    }

    /// <summary>AC-007: a material is an empty cell for every row, even with a submission record.</summary>
    [Fact]
    public async Task Material_IsEmpty_WithoutSubmission()
    {
        var cell = await CellAsync(null, maxPoints: null, material: true);

        Assert.Equal(JournalCellState.Empty, cell.State);
    }

    /// <summary>AC-007: a material is an empty cell even when a submission record exists.</summary>
    [Fact]
    public async Task Material_IsEmpty_WithSubmission()
    {
        var cell = await CellAsync(
            (s, i, m) => s.AddSubmission(i, m, SubmissionState.TurnedIn, assignedGrade: 5m), maxPoints: null, material: true);

        Assert.Equal(JournalCellState.Empty, cell.State);
    }
}
