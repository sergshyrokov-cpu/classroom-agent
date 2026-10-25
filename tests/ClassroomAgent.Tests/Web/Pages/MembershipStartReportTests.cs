using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using ClosedXML.Excel;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Blank = ClassroomAgent.Tests.TestInfrastructure.GroupLessonsBlank;
using Ms = ClassroomAgent.Tests.TestInfrastructure.MembershipStartHost;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-056 AC-002 (spec FR-001 a, FR-003.2): the teacher on the roster when the installation's first run loaded the course
/// is named in the teacher line of a template report for a period before that run — on screen, in the table export and
/// in the shipped blank form. The course and its roster come from a real synchronization run (TC-2), Google substituted
/// (TC-4).
/// </summary>
public sealed class MembershipStartReportTests(PostgreSqlFixture database)
{
    private const string From = "2025-09-01";

    private const string To = "2025-12-31";

    /// <summary>No profile name parts, so every report names the teacher by the address before <c>@</c> (US-042 FR-003).</summary>
    private const string TeacherLabel = "teacher.alpha";

    private static readonly string CourseGoogleId = CourseTestData.CourseId(1);

    private static RosterEntry Teacher => new(CourseTestData.UserId(1), CourseTestData.Email(TeacherLabel), "Test Teacher Alpha");

    /// <summary>Course created 2025-09-01 with teacher T and one student, one graded item in October 2025.</summary>
    private static void CourseInGoogle(FakeClassroomReader reader) =>
        reader
            .WithCourse(
                CourseGoogleId,
                CourseTestData.CourseName(1),
                teachers: [Teacher],
                students: [Ms.Person(2)],
                creationTime: Ms.CourseCreated)
            .WithCourseWork(
                CourseGoogleId, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1),
                Ms.October2025Item, maxPoints: 10m);

    private static Dictionary<string, string?> TableBody(long courseId)
    {
        var body = JournalExportHostExtensions.September(courseId);
        body["from"] = From;
        body["to"] = To;
        return body;
    }

    private static Dictionary<string, object?> BlankBody(long courseId)
    {
        var body = Blank.Body(courseId);
        body["from"] = From;
        body["to"] = To;
        return body;
    }

    private static bool AnyCellContains(XLWorkbook workbook, string text) =>
        workbook.Worksheets.Any(sheet => sheet.CellsUsed().Any(cell => cell.GetString().Contains(text, StringComparison.Ordinal)));

    /// <summary>AC-002, FR-003.2: the report page names T in the teacher line, next to the October 2025 item.</summary>
    [Fact]
    public async Task TheReportBeforeTheFirstRun_NamesTheFirstLoadsTeacher_OnScreen()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Ms.StartFirstRunAsync(database, CourseInGoogle, ct);
        var client = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);
        var courseId = await host.CourseIdAsync(CourseGoogleId, ct);

        var page = await client.GetAsync(
            ReportTemplateTestData.ReportUrl(ReportTemplateTestData.BuiltInKey, courseId, From, To), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        // The control: the report is shown with its item, so the absence it replaces is not an empty report.
        Assert.Contains(CourseWorkTestData.Title(1), page.Text, StringComparison.Ordinal);
        Assert.Contains(TeacherLabel, page.Text, StringComparison.Ordinal);
    }

    /// <summary>AC-002, FR-003.2: the table export (US-028) of the same report names T.</summary>
    [Fact]
    public async Task TheReportBeforeTheFirstRun_NamesTheFirstLoadsTeacher_InTheTableExport()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Ms.StartFirstRunAsync(database, CourseInGoogle, ct);
        var client = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);
        var courseId = await host.CourseIdAsync(CourseGoogleId, ct);

        var response = await client.ExportAsync(TableBody(courseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        Assert.True(AnyCellContains(workbook, CourseWorkTestData.Title(1)), "The export must carry the October 2025 item.");
        Assert.True(AnyCellContains(workbook, TeacherLabel), "The teacher line of the table export must name the first load's teacher.");
    }

    /// <summary>AC-002, FR-003.2: the shipped blank form («Бланк для групових занять», US-043) names T.</summary>
    [Fact]
    public async Task TheReportBeforeTheFirstRun_NamesTheFirstLoadsTeacher_InTheBlankForm()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Ms.StartFirstRunAsync(database, CourseInGoogle, ct);
        var client = await JournalHostExtensions.SignInAsync(host, Actor.Dean, ct);
        var courseId = await host.CourseIdAsync(CourseGoogleId, ct);

        var response = await client.ExportAsync(BlankBody(courseId), ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        using var workbook = response.Workbook();
        GroupLessonsBlankHostExtensions.AssertIsTheForm(workbook);
        Assert.True(AnyCellContains(workbook, CourseWorkTestData.Title(1)), "The form must carry the October 2025 item.");
        Assert.True(
            workbook.Worksheet(Blank.TopicsSheet).CellsUsed().Any(c => c.GetString().Contains(TeacherLabel, StringComparison.Ordinal)),
            "The teachers cell of the form must name the first load's teacher.");
    }
}
