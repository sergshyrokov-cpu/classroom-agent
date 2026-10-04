using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 AC-004, AC-005, AC-012 (spec FR-005, FR-006, FR-015): what a report holds — the columns and their order, the
/// student rows, the header with the teachers of the period, the lesson topics, the empty states, the difference
/// between a copy and the built-in template, and the dating of columns in the school's time zone.
/// </summary>
public sealed class ReportContentTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    private static DateTimeOffset Noon(int day) => new(2026, 9, day, 9, 0, 0, TimeSpan.Zero);

    private static async Task<Report> ReportAsync(ReportTemplateWorld world, long course, string template = ReportTemplateTestData.BuiltInKey)
    {
        var request = new ReportRequest(
            [template],
            [course.ToString(CultureInfo.InvariantCulture)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText]);
        var result = await world.Report.ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);
        Assert.NotNull(result.Page.Report);
        return result.Page.Report;
    }

    /// <summary>AC-004: columns are the lessons of the period without materials, by date, then title, then id.</summary>
    [Fact]
    public async Task TheBuiltInReport_HasNoMaterialColumns_AndOrdersByLessonDateThenTitle()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        world.Fields.AddLesson(course, Noon(10), "Beta");
        world.Fields.AddLesson(course, Noon(10), "Alpha");
        world.Fields.AddLesson(course, Noon(3), "Zeta");
        world.Fields.AddLesson(course, Noon(4), "Reading", resource: CourseWorkResource.CourseWorkMaterial, maxPoints: null);

        var report = await ReportAsync(world, course);

        Assert.Equal(
            new[]
            {
                new GradingColumn(new DateOnly(2026, 9, 3), "Zeta", false),
                new GradingColumn(new DateOnly(2026, 9, 10), "Alpha", false),
                new GradingColumn(new DateOnly(2026, 9, 10), "Beta", false),
            },
            report.Grading!.Columns);
        Assert.Equal(ReportView.Short, report.View);
    }

    /// <summary>AC-004: lessons with the same date and title follow the id; materials are columns when shown.</summary>
    [Fact]
    public async Task ColumnsWithTheSameDateAndTitle_FollowTheId_AndMaterialsAreShownWhenAsked()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        var student = world.Fields.AddMember(course);
        var first = world.Fields.AddLesson(course, Noon(10), "Same", maxPoints: 10m);
        var second = world.Fields.AddLesson(course, Noon(10), "Same", maxPoints: 10m);
        world.Fields.AddLesson(course, Noon(11), "Reading", resource: CourseWorkResource.CourseWorkMaterial, maxPoints: null);
        world.Fields.AddSubmission(first, student, assignedGrade: 3m);
        world.Fields.AddSubmission(second, student, assignedGrade: 7m);
        var template = world.SeedTemplate("Test Template One", ReportTemplateTestData.Settings(hideMaterials: false));

        var report = await ReportAsync(world, course, template.Id.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(3, report.Grading!.Columns.Count);
        Assert.True(report.Grading.Columns[2].IsMaterial);
        Assert.Equal(3m, report.Grading.Rows.Single().Cells[0].Grade!.Points);
        Assert.Equal(7m, report.Grading.Rows.Single().Cells[1].Grade!.Points);
    }

    /// <summary>AC-004: rows are the students of the period (BR-051), a leaver who submitted included, teachers never.</summary>
    [Fact]
    public async Task Rows_AreTheStudentsOfThePeriod()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        var lesson = world.Fields.AddLesson(course, Noon(10));
        world.Fields.AddMember(course, fullName: "Олена Тестова");
        world.Fields.AddMember(course, fullName: "Андрій Тестовий");
        world.Fields.AddMember(course, fullName: null, email: null);
        world.Fields.AddMember(course, ClassroomRole.Teacher, fullName: "Test Teacher One");
        world.Fields.AddMember(
            course,
            fullName: "Leaver Without Work",
            firstSeenAt: JournalTestData.Period.StartUtc.AddDays(-60),
            lastSeenAt: JournalTestData.Period.StartUtc.AddDays(-10),
            onRoster: false);
        var submitter = world.Fields.AddMember(
            course,
            fullName: "Leaver With Work",
            firstSeenAt: JournalTestData.Period.StartUtc.AddDays(-60),
            lastSeenAt: JournalTestData.Period.StartUtc.AddDays(-10),
            onRoster: false);
        world.Fields.AddMember(course, fullName: "Joined Later", firstSeenAt: JournalTestData.Period.EndUtc.AddDays(2));
        world.Fields.AddSubmission(lesson, submitter);

        var report = await ReportAsync(world, course);

        Assert.Equal(
            new[]
            {
                new PersonName("Андрій Тестовий", JournalNameKind.FullName),
                new PersonName("Leaver With Work", JournalNameKind.FullName),
                new PersonName("Олена Тестова", JournalNameKind.FullName),
                new PersonName(null, JournalNameKind.Unnamed),
            }.Select(p => p.DisplayName).OrderBy(n => n is null).ThenBy(n => n, StringComparer.Create(Uk, false)),
            report.Grading!.Rows.Select(r => r.Student.DisplayName));
        Assert.Equal(JournalNameKind.Unnamed, report.Grading.Rows[^1].Student.NameKind);
        Assert.Null(report.Grading.Rows[^1].Student.DisplayName);
        Assert.All(report.Grading.Rows, r => Assert.Single(r.Cells));
    }

    /// <summary>AC-004: the header names the course, the period and the teachers of the period; a teacher who left is absent.</summary>
    [Fact]
    public async Task TheHeader_NamesTheCourseThePeriodAndTheTeachersOfThePeriod()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse("Test Course One", "Test Section A");
        world.Fields.AddLesson(course, Noon(10));
        world.Fields.AddMember(course, ClassroomRole.Teacher, fullName: "Test Teacher Zed");
        world.Fields.AddMember(course, ClassroomRole.Teacher, fullName: "Test Teacher Alpha");
        world.Fields.AddMember(course, ClassroomRole.Teacher, fullName: null, email: null);
        world.Fields.AddMember(
            course,
            ClassroomRole.Teacher,
            fullName: "Test Teacher Gone",
            firstSeenAt: JournalTestData.Period.StartUtc.AddDays(-60),
            lastSeenAt: JournalTestData.Period.StartUtc.AddDays(-10),
            onRoster: false);

        var report = await ReportAsync(world, course);

        Assert.True(report.Header.TemplateIsBuiltIn);
        Assert.Null(report.Header.TemplateName);
        Assert.Equal("Test Course One", report.Header.CourseName);
        Assert.Equal("Test Section A", report.Header.CourseSection);
        Assert.Equal(JournalTestData.Period.From, report.Header.From);
        Assert.Equal(JournalTestData.Period.To, report.Header.To);
        Assert.Equal(
            new[]
            {
                new PersonName("Test Teacher Alpha", JournalNameKind.FullName),
                new PersonName("Test Teacher Zed", JournalNameKind.FullName),
                new PersonName(null, JournalNameKind.Unnamed),
            },
            report.Header.Teachers);
    }

    /// <summary>AC-004: a created template is named in the header as it was written.</summary>
    [Fact]
    public async Task ACreatedTemplate_IsNamedInTheHeader_AsWritten()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        world.Fields.AddLesson(course, Noon(10));
        var template = world.SeedTemplate("Test Template One");

        var report = await ReportAsync(world, course, template.Id.ToString(CultureInfo.InvariantCulture));

        Assert.False(report.Header.TemplateIsBuiltIn);
        Assert.Equal("Test Template One", report.Header.TemplateName);
    }

    /// <summary>AC-004: lesson topics list every non-material lesson, ungraded too, with the template's hours.</summary>
    [Fact]
    public async Task LessonTopics_ListEveryNonMaterialItem_WithTheTemplatesHours()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        world.Fields.AddLesson(course, Noon(12), "Graded Lesson");
        world.Fields.AddLesson(course, Noon(8), "Ungraded Lesson", maxPoints: null);
        world.Fields.AddLesson(course, Noon(9), "Reading", resource: CourseWorkResource.CourseWorkMaterial, maxPoints: null);
        var template = world.SeedTemplate(
            "Test Template One", ReportTemplateTestData.Settings(hideMaterials: false, hours: 4));

        var report = await ReportAsync(world, course, template.Id.ToString(CultureInfo.InvariantCulture));
        var builtIn = await ReportAsync(world, course);

        Assert.Equal(
            new[]
            {
                new LessonTopicRow(new DateOnly(2026, 9, 8), "Ungraded Lesson", 4),
                new LessonTopicRow(new DateOnly(2026, 9, 12), "Graded Lesson", 4),
            },
            report.LessonTopics!.Rows);
        Assert.Equal(3, report.Grading!.Columns.Count);
        Assert.Equal(
            new[]
            {
                new LessonTopicRow(new DateOnly(2026, 9, 8), "Ungraded Lesson", 2),
                new LessonTopicRow(new DateOnly(2026, 9, 12), "Graded Lesson", 2),
            },
            builtIn.LessonTopics!.Rows);
    }

    /// <summary>AC-004: with no lesson in the period the empty state replaces both parts.</summary>
    [Fact]
    public async Task NothingPublished_ReplacesBothParts()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        world.Fields.AddMember(course);
        world.Fields.AddLesson(course, new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

        var report = await ReportAsync(world, course);

        Assert.Equal(ReportEmptyStateKey.NothingPublished, report.EmptyStateKey);
        Assert.Null(report.Grading);
        Assert.Null(report.LessonTopics);
    }

    /// <summary>AC-004: lessons but no student keep the columns and the lesson topics.</summary>
    [Fact]
    public async Task NoStudents_KeepsLessonTopics()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        world.Fields.AddMember(course, ClassroomRole.Teacher);
        world.Fields.AddLesson(course, Noon(10), "Only Lesson");

        var report = await ReportAsync(world, course);

        Assert.Null(report.EmptyStateKey);
        Assert.Single(report.Grading!.Columns);
        Assert.Empty(report.Grading.Rows);
        Assert.Single(report.LessonTopics!.Rows);
    }

    /// <summary>AC-005: a copy's own settings change its report; the built-in report of the same data is unchanged.</summary>
    [Fact]
    public async Task ACopysSettings_ShowInItsReport_AndTheBuiltInIsUnchanged()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        var student = world.Fields.AddMember(course);
        var graded = world.Fields.AddLesson(course, Noon(5), "Graded");
        var turnedIn = world.Fields.AddLesson(course, Noon(6), "Turned In");
        world.Fields.AddLesson(course, Noon(7), "Unassigned");
        world.Fields.AddLesson(course, Noon(8), "Reading", resource: CourseWorkResource.CourseWorkMaterial, maxPoints: null);
        world.Fields.AddSubmission(graded, student, assignedGrade: 8.5m);
        world.Fields.AddSubmission(
            turnedIn, student, late: true, turnedInAt: new DateTimeOffset(2026, 9, 5, 22, 30, 0, TimeSpan.Zero));

        var builtInBefore = await ReportAsync(world, course);
        var copy = world.SeedTemplate(
            "Test Template One",
            ReportTemplateTestData.Settings(
                view: ReportView.Full,
                hideMaterials: false,
                hours: 4,
                marks: new Dictionary<ReportCellState, ReportMark>
                {
                    [ReportCellState.TurnedInNotGraded] = new(ReportMarkKind.Own, "HANDED"),
                    [ReportCellState.NotAssigned] = new(ReportMarkKind.Own, "N/A"),
                },
                late: new ReportLateMark(ReportLateMarkKind.Own, "LATE!")));
        var copyReport = await ReportAsync(world, course, copy.Id.ToString(CultureInfo.InvariantCulture));
        var builtInAfter = await ReportAsync(world, course);

        foreach (var builtIn in new[] { builtInBefore, builtInAfter })
        {
            Assert.Equal(ReportView.Short, builtIn.View);
            Assert.Equal(3, builtIn.Grading!.Columns.Count);
            Assert.All(builtIn.LessonTopics!.Rows, r => Assert.Equal(2, r.Hours));
            var cells = builtIn.Grading.Rows.Single().Cells;
            Assert.Equal(new ReportGrade(ReportGradeKind.ScaleLabel, "11", null, null), cells[0].Grade);
            Assert.Equal(ReportCellContent.Empty, cells[1].Content);
            Assert.Null(cells[1].Late);
            Assert.Null(cells[1].TurnedInOn);
            Assert.Equal(new ReportCellMark(ReportCellMarkKind.Own, null, "—"), cells[2].Mark);
        }

        Assert.Equal(ReportView.Full, copyReport.View);
        Assert.Equal(4, copyReport.Grading!.Columns.Count);
        Assert.All(copyReport.LessonTopics!.Rows, r => Assert.Equal(4, r.Hours));
        var copyCells = copyReport.Grading.Rows.Single().Cells;
        Assert.Equal(new ReportGrade(ReportGradeKind.RawPoints, null, 8.5m, 10m), copyCells[0].Grade);
        Assert.Equal(new ReportCellMark(ReportCellMarkKind.Own, null, "HANDED"), copyCells[1].Mark);
        Assert.Equal(new ReportCellMark(ReportCellMarkKind.Own, null, "LATE!"), copyCells[1].Late);
        Assert.Equal(new DateOnly(2026, 9, 6), copyCells[1].TurnedInOn);
        Assert.Equal(new ReportCellMark(ReportCellMarkKind.Own, null, "N/A"), copyCells[2].Mark);
    }

    /// <summary>AC-012: a lesson just after local midnight belongs to the local day, in the period and dated by it.</summary>
    [Fact]
    public async Task ColumnsAreDatedByTheLessonDate_InTheSchoolsZone()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        world.Fields.AddMember(course);
        world.Fields.AddLesson(course, new DateTimeOffset(2026, 8, 31, 21, 30, 0, TimeSpan.Zero), "Local First Of September");
        world.Fields.AddLesson(course, new DateTimeOffset(2026, 9, 30, 21, 30, 0, TimeSpan.Zero), "Local First Of October");

        var report = await ReportAsync(world, course);

        Assert.Equal(
            new[] { new GradingColumn(new DateOnly(2026, 9, 1), "Local First Of September", false) },
            report.Grading!.Columns);
        Assert.NotEmpty(world.Fields.Intervals);
        Assert.All(
            world.Fields.Intervals,
            i => Assert.Equal((JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc), i));
    }
}
