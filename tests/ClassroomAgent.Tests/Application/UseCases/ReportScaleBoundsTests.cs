using System.Globalization;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-057 spec FR-005, AC-006, AC-007 b), AC-008: the school-scale percent is bounded before it is divided — points not
/// less than the maximum give 100 %, points not greater than 0 give 0 % — so no pair overflows, through both use cases
/// that build a report with <c>ReportBuilder</c>: the on-screen report (<c>GetReportQuery</c>) and the export
/// (<c>ExportJournalCommand</c>). The journal port is substituted (TC-1), which is what lets AC-007 b) deliver a negative
/// pair no table can hold (OD-001). Every value is synthetic.
/// </summary>
public sealed class ReportScaleBoundsTests
{
    private const string Zero = "TSZERO";

    private const string Middle = "TSMID";

    private const string Full = "TSFULL";

    /// <summary>One label per bound, so the label shown names the percent unambiguously.</summary>
    private static readonly IReadOnlyList<(int From, int To, string Label)> BoundsScale =
        [(0, 0, Zero), (1, 99, Middle), (100, 100, Full)];

    private static readonly DateTimeOffset LessonDate = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    /// <summary>Which grade of the submission carries the points: the assigned grade or, alone, the draft.</summary>
    public static TheoryData<string> Grades => new("assigned", "draft");

    /// <summary>
    /// One course, one student, one lesson with <paramref name="max"/>, and the student's submission carrying
    /// <paramref name="points"/> as <paramref name="grade"/>, under a template of <paramref name="scale"/> in the full view
    /// (which shows the draft). Returns the course and the template reference.
    /// </summary>
    private static (long Course, string Template) Seed(
        ReportTemplateWorld world,
        string grade,
        decimal points,
        decimal max,
        IReadOnlyList<(int From, int To, string Label)> scale)
    {
        var course = world.Fields.AddCourse();
        var student = world.Fields.AddMember(course);
        var lesson = world.Fields.AddLesson(course, LessonDate, "Test Lesson", max);
        world.Fields.AddSubmission(
            lesson,
            student,
            grade == "assigned" ? SubmissionState.Returned : SubmissionState.TurnedIn,
            assignedGrade: grade == "assigned" ? points : null,
            draftGrade: grade == "draft" ? points : null);
        var template = world.SeedTemplate("Test Template Bounds", ReportTemplateTestData.Settings(ReportView.Full, scale: scale));
        return (course, template.Id.ToString(CultureInfo.InvariantCulture));
    }

    private static async Task<ReportCell> PageCellAsync(ReportTemplateWorld world, long course, string template)
    {
        var result = await world.Report.ExecuteAsync(
            new ReportRequest(
                [template],
                [course.ToString(CultureInfo.InvariantCulture)],
                [JournalTestData.Period.FromText],
                [JournalTestData.Period.ToText]),
            Uk,
            TestContext.Current.CancellationToken);
        Assert.NotNull(result.Page.Report);
        return result.Page.Report.Grading!.Rows.Single().Cells.Single();
    }

    /// <summary>The label of the cell's grade, or of its draft when that is the grade that carries the points.</summary>
    private static ReportGrade? GradeOf(ReportCell cell, string grade) => grade == "assigned" ? cell.Grade : cell.DraftGrade;

    /// <summary>The page's report for the pair: the cell shows <paramref name="label"/> as a scale label.</summary>
    private static async Task AssertPageShowsAsync(string grade, decimal points, decimal max, string label)
    {
        var world = new ReportTemplateWorld();
        var (course, template) = Seed(world, grade, points, max, BoundsScale);

        var cell = await PageCellAsync(world, course, template);

        Assert.Equal(new ReportGrade(ReportGradeKind.ScaleLabel, label, null, null), GradeOf(cell, grade));
    }

    /// <summary>The export of the pair is produced and its workbook carries <paramref name="label"/> and no other label.</summary>
    private static async Task AssertExportShowsAsync(string grade, decimal points, decimal max, string label)
    {
        var world = new JournalExportWorld();
        var (course, template) = Seed(world.Report, grade, points, max, BoundsScale);

        var result = await world.RunAsync(JournalExportWorld.Request(course, template: template));

        Assert.Equal(JournalExportOutcome.Exported, result.Outcome);
        var workbook = JsonSerializer.Serialize(Assert.Single(world.Renderer.Rendered));
        Assert.Contains(label, workbook, StringComparison.Ordinal);
        foreach (var other in new[] { Zero, Middle, Full }.Where(l => l != label))
        {
            Assert.DoesNotContain(other, workbook, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC-006, FR-005.1: 7·10²⁸ points against a maximum of 0.0001 show the label for 100 % on the page — before the
    /// change the division overflows and the report fails.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grades))]
    public Task AnExtremeGrade_ShowsTheLabelFor100Percent_OnThePage(string grade) =>
        AssertPageShowsAsync(grade, 70_000_000_000_000_000_000_000_000_000m, 0.0001m, Full);

    /// <summary>AC-006, FR-005.1: the same pair in the export — it is produced and carries the label for 100 %.</summary>
    [Theory]
    [MemberData(nameof(Grades))]
    public Task AnExtremeGrade_ShowsTheLabelFor100Percent_InTheExport(string grade) =>
        AssertExportShowsAsync(grade, 70_000_000_000_000_000_000_000_000_000m, 0.0001m, Full);

    /// <summary>
    /// AC-007 b), FR-005.2, OD-001: −7·10²⁸ points against a maximum of 0.0001, delivered by the substituted journal
    /// port (no stored row can hold it), show the label for 0 % on the page and do not fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(Grades))]
    public Task ANegativeExtremeGrade_ShowsTheLabelFor0Percent_OnThePage(string grade) =>
        AssertPageShowsAsync(grade, -70_000_000_000_000_000_000_000_000_000m, 0.0001m, Zero);

    /// <summary>AC-007 b), FR-005.2: the same negative pair in the export — produced, with the label for 0 %.</summary>
    [Theory]
    [MemberData(nameof(Grades))]
    public Task ANegativeExtremeGrade_ShowsTheLabelFor0Percent_InTheExport(string grade) =>
        AssertExportShowsAsync(grade, -70_000_000_000_000_000_000_000_000_000m, 0.0001m, Zero);

    /// <summary>
    /// FR-005.1, FR-005.2: the bounds themselves — points equal to the maximum give 100 %, points of 0 give 0 %, and a
    /// grade between them keeps today's computation (1 of 2 is 50 %, a middle label). Green before the change by design.
    /// </summary>
    [Theory]
    [InlineData("assigned", 2, Full)]
    [InlineData("assigned", 0, Zero)]
    [InlineData("assigned", 1, Middle)]
    [InlineData("draft", 2, Full)]
    [InlineData("draft", 0, Zero)]
    public Task TheBounds_AndAPointBetween_ShowTheirLabels(string grade, int points, string label) =>
        AssertPageShowsAsync(grade, points, 2m, label);

    /// <summary>
    /// AC-008, FR-005.3: the 12-point preset — 6, 12 and 0 points of 12 show "6", "12" and "1" (50 %, 100 %, 0 %), as before
    /// this Story, on the page; the export of the same report is produced. That the workbook is the page's report mapped
    /// once is US-028's <c>TheWorkbook_IsTheMappedReportOfThePage_ForTheSameInputs</c> — a one-character label is not
    /// searched for in the workbook, where other cells could hold the same text. Green before the change by design.
    /// </summary>
    [Theory]
    [InlineData(6, "6")]
    [InlineData(12, "12")]
    [InlineData(0, "1")]
    public async Task TheTwelvePointPreset_ShowsTheSameLabelsAsBefore(int points, string label)
    {
        var world = new JournalExportWorld();
        var (course, template) = Seed(world.Report, "assigned", points, 12m, ReportTemplateTestData.TwelvePoint);

        var cell = await PageCellAsync(world.Report, course, template);
        var result = await world.RunAsync(JournalExportWorld.Request(course, template: template));

        Assert.Equal(new ReportGrade(ReportGradeKind.ScaleLabel, label, null, null), cell.Grade);
        Assert.Equal(JournalExportOutcome.Exported, result.Outcome);
        Assert.Single(world.Renderer.Rendered);
    }
}
