using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 AC-003, AC-005, AC-013 (spec FR-005.4, FR-015): one report cell — the mark of every state as the template
/// says, a grade through the scale or as raw points, the rounding at a range boundary, the full and short views, the
/// late mark, an unrecognised state and a material.
/// </summary>
public sealed class ReportCellTests
{
    private const string RawUnrecognised = "SOME_NEW_STATE";

    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    private static readonly DateTimeOffset LessonDate = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DueBeforeB = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DueAfterB = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<ReportCellState, ReportMarkKind> StatesAndKinds
    {
        get
        {
            var data = new TheoryData<ReportCellState, ReportMarkKind>();
            foreach (var state in ReportTemplateTestData.States)
            {
                foreach (var kind in Enum.GetValues<ReportMarkKind>())
                {
                    data.Add(state, kind);
                }
            }

            return data;
        }
    }

    private sealed record Submission(
        SubmissionState State = SubmissionState.TurnedIn,
        decimal? Grade = null,
        decimal? Draft = null,
        bool Late = false,
        DateTimeOffset? TurnedInAt = null,
        string? RawState = null);

    private static ReportTemplateSettings WithMark(ReportCellState state, ReportMark mark) =>
        ReportTemplateTestData.Settings(marks: new Dictionary<ReportCellState, ReportMark> { [state] = mark });

    private static async Task<ReportCell> CellAsync(
        ReportTemplateSettings settings,
        Submission? submission,
        decimal? max = 10m,
        DateTimeOffset? due = null,
        CourseWorkResource resource = CourseWorkResource.CourseWork)
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        var student = world.Fields.AddMember(course);
        var lesson = world.Fields.AddLesson(course, LessonDate, "Test Lesson", max, due, resource);
        if (submission is not null)
        {
            world.Fields.AddSubmission(
                lesson,
                student,
                submission.State,
                submission.Grade,
                submission.Draft,
                submission.Late,
                submission.TurnedInAt,
                submission.RawState);
        }

        var template = world.SeedTemplate("Test Template One", settings);
        var request = new ReportRequest(
            [template.Id.ToString(CultureInfo.InvariantCulture)],
            [course.ToString(CultureInfo.InvariantCulture)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText]);
        var result = await world.Report.ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);
        Assert.NotNull(result.Page.Report);
        return result.Page.Report.Grading!.Rows.Single().Cells.Single();
    }

    private static Task<ReportCell> CellOfStateAsync(ReportCellState state, ReportTemplateSettings settings) => state switch
    {
        ReportCellState.TurnedInNotGraded => CellAsync(settings, new Submission(SubmissionState.TurnedIn)),
        ReportCellState.ReturnedWithoutGrade => CellAsync(settings, new Submission(SubmissionState.Returned)),
        ReportCellState.TurnedIn => CellAsync(settings, new Submission(SubmissionState.TurnedIn), max: null),
        ReportCellState.Returned => CellAsync(settings, new Submission(SubmissionState.Returned), max: null),
        ReportCellState.NotTurnedIn => CellAsync(settings, new Submission(SubmissionState.Created), due: DueBeforeB),
        ReportCellState.NotDueYet => CellAsync(settings, new Submission(SubmissionState.Created), due: DueAfterB),
        ReportCellState.NotTurnedInNoDueDate => CellAsync(settings, new Submission(SubmissionState.Created)),
        ReportCellState.NotAssigned => CellAsync(settings, null),
        _ => CellAsync(settings, new Submission(SubmissionState.Unrecognised, RawState: RawUnrecognised)),
    };

    /// <summary>AC-005: each state shows the template's choice — the program key, its own text or nothing.</summary>
    [Theory]
    [MemberData(nameof(StatesAndKinds))]
    public async Task EveryStateMark_IsShownAsTheTemplateSays(ReportCellState state, ReportMarkKind kind)
    {
        var mark = new ReportMark(kind, kind == ReportMarkKind.Own ? "OWN-TEXT" : null);

        var cell = await CellOfStateAsync(state, WithMark(state, mark));

        switch (kind)
        {
            case ReportMarkKind.Program when state == ReportCellState.Unrecognised:
                Assert.Equal(ReportCellContent.RawState, cell.Content);
                Assert.Equal(RawUnrecognised, cell.RawState);
                Assert.Null(cell.Mark);
                break;
            case ReportMarkKind.Program:
                Assert.Equal(ReportCellContent.Mark, cell.Content);
                Assert.Equal(new ReportCellMark(ReportCellMarkKind.Program, state.ToString(), null), cell.Mark);
                break;
            case ReportMarkKind.Own:
                Assert.Equal(ReportCellContent.Mark, cell.Content);
                Assert.Equal(new ReportCellMark(ReportCellMarkKind.Own, null, "OWN-TEXT"), cell.Mark);
                break;
            default:
                Assert.Equal(ReportCellContent.Empty, cell.Content);
                Assert.Null(cell.Mark);
                break;
        }

        Assert.Null(cell.Grade);
    }

    /// <summary>AC-003: with a scale an assigned grade shows the label of the range its percent falls in.</summary>
    [Theory]
    [InlineData(4, "low")]
    [InlineData(5, "high")]
    public async Task AGrade_IsShownThroughTheScale(int points, string label)
    {
        var settings = ReportTemplateTestData.Settings(scale: ReportTemplateTestData.TwoRow);

        var cell = await CellAsync(settings, new Submission(Grade: points));

        Assert.Equal(ReportCellContent.Grade, cell.Content);
        Assert.Equal(new ReportGrade(ReportGradeKind.ScaleLabel, label, null, null), cell.Grade);
        Assert.Null(cell.Mark);
    }

    /// <summary>AC-003, AC-013: with no conversion the grade is the raw points, zero included.</summary>
    [Theory]
    [InlineData(8.5)]
    [InlineData(0)]
    public async Task NoConversion_ShowsRawPoints(double points)
    {
        var grade = (decimal)points;

        var cell = await CellAsync(ReportTemplateTestData.Settings(), new Submission(Grade: grade));

        Assert.Equal(ReportCellContent.Grade, cell.Content);
        Assert.Equal(new ReportGrade(ReportGradeKind.RawPoints, null, grade, 10m), cell.Grade);
    }

    /// <summary>AC-013: the percent rounds to a whole number, .5 up, and is clamped to 0–100 before the range is chosen.</summary>
    [Theory]
    [InlineData(0.85, "2")]
    [InlineData(0.84, "1")]
    [InlineData(10.5, "12")]
    [InlineData(10, "12")]
    [InlineData(0, "1")]
    public async Task RoundingAtARangeBoundary_FollowsAc013(double points, string label)
    {
        var settings = ReportTemplateTestData.Settings(scale: ReportTemplateTestData.TwelvePoint);

        var cell = await CellAsync(settings, new Submission(Grade: (decimal)points));

        Assert.Equal(new ReportGrade(ReportGradeKind.ScaleLabel, label, null, null), cell.Grade);
    }

    /// <summary>AC-013: a maximum of zero cannot give a percent, so the raw points are shown even with ranges.</summary>
    [Fact]
    public async Task AZeroMaximum_ShowsRawPoints()
    {
        var settings = ReportTemplateTestData.Settings(scale: ReportTemplateTestData.TwelvePoint);

        var cell = await CellAsync(settings, new Submission(Grade: 3m), max: 0m);

        Assert.Equal(ReportCellContent.Grade, cell.Content);
        Assert.Equal(new ReportGrade(ReportGradeKind.RawPoints, null, 3m, 0m), cell.Grade);
    }

    /// <summary>AC-005: the full view adds the draft grade and the turn-in date; the short view adds neither.</summary>
    [Fact]
    public async Task TheFullView_AddsDraftAndTurnInDate_TheShortViewDoesNot()
    {
        var submission = new Submission(
            Draft: 6.25m, TurnedInAt: new DateTimeOffset(2026, 9, 5, 22, 30, 0, TimeSpan.Zero));

        var full = await CellAsync(
            ReportTemplateTestData.Settings(ReportView.Full, scale: ReportTemplateTestData.TwoRow), submission);
        var shortView = await CellAsync(
            ReportTemplateTestData.Settings(ReportView.Short, scale: ReportTemplateTestData.TwoRow), submission);

        Assert.Equal(new ReportGrade(ReportGradeKind.ScaleLabel, "high", null, null), full.DraftGrade);
        Assert.Equal(new DateOnly(2026, 9, 6), full.TurnedInOn);
        Assert.Null(shortView.DraftGrade);
        Assert.Null(shortView.TurnedInOn);
    }

    /// <summary>AC-005: the late mark is the program's, the template's own text or hidden.</summary>
    [Theory]
    [InlineData(ReportLateMarkKind.Program)]
    [InlineData(ReportLateMarkKind.Own)]
    [InlineData(ReportLateMarkKind.Hidden)]
    public async Task TheLateMark_FollowsTheTemplate(ReportLateMarkKind kind)
    {
        var settings = ReportTemplateTestData.Settings(
            late: new ReportLateMark(kind, kind == ReportLateMarkKind.Own ? "LATE!" : null));

        var cell = await CellAsync(settings, new Submission(Late: true));

        var expected = kind switch
        {
            ReportLateMarkKind.Program => new ReportCellMark(ReportCellMarkKind.Program, "Late", null),
            ReportLateMarkKind.Own => new ReportCellMark(ReportCellMarkKind.Own, null, "LATE!"),
            _ => null,
        };
        Assert.Equal(expected, cell.Late);
    }

    /// <summary>AC-005: a cell with no submission never carries a late mark.</summary>
    [Theory]
    [InlineData(ReportLateMarkKind.Program)]
    [InlineData(ReportLateMarkKind.Own)]
    public async Task ANotAssignedCell_NeverHasALateMark(ReportLateMarkKind kind)
    {
        var settings = ReportTemplateTestData.Settings(
            late: new ReportLateMark(kind, kind == ReportLateMarkKind.Own ? "LATE!" : null));

        var cell = await CellAsync(settings, null);

        Assert.Null(cell.Late);
    }

    /// <summary>AC-005: an unrecognised state with the program's choice shows Google's raw value and never a grade.</summary>
    [Fact]
    public async Task AnUnrecognisedState_ShowsTheRawValue_WithTheProgramsChoice()
    {
        var submission = new Submission(SubmissionState.Unrecognised, Grade: 7m, RawState: RawUnrecognised);

        var cell = await CellAsync(ReportTemplateTestData.Settings(), submission);

        Assert.Equal(ReportCellContent.RawState, cell.Content);
        Assert.Equal(RawUnrecognised, cell.RawState);
        Assert.Null(cell.Grade);
        Assert.Null(cell.Mark);
    }

    /// <summary>AC-005: a material cell is empty whatever the template shows, even when materials are shown.</summary>
    [Fact]
    public async Task AMaterialCell_IsAlwaysEmpty()
    {
        var settings = ReportTemplateTestData.Settings(
            ReportView.Full,
            hideMaterials: false,
            late: new ReportLateMark(ReportLateMarkKind.Program, null));
        var submission = new Submission(
            Late: true, TurnedInAt: new DateTimeOffset(2026, 9, 5, 22, 30, 0, TimeSpan.Zero));

        var cell = await CellAsync(settings, submission, max: null, resource: CourseWorkResource.CourseWorkMaterial);

        Assert.Equal(new ReportCell(ReportCellContent.Empty, null, null, null, null, null, null), cell);
    }
}
