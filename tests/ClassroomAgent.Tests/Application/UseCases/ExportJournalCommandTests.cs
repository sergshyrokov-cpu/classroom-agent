using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-028 <c>ExportJournalCommand</c> over substituted ports (TC-1; spec FR-001, FR-003, FR-005, FR-006, FR-009,
/// FR-010; VR-001; entity model §3.4): validation before any read, not-found, one source of content with the report
/// page, the file and its name, the one audit row, and read-only mode.
/// </summary>
public sealed class ExportJournalCommandTests
{
    private static void AssertNothingHappened(JournalExportWorld world)
    {
        Assert.Empty(world.Renderer.Rendered);
        Assert.Empty(world.Report.Audit.Written);
        Assert.Empty(world.Work.Declared);
    }

    /// <summary>AC-001, FR-006: a valid request answers the rendered bytes under the FR-006.1 name.</summary>
    [Fact]
    public async Task AValidRequest_ExportsTheRenderedFile_UnderTheCourseAndPeriodName()
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse(name: "Test Course One");

        var result = await world.RunAsync(JournalExportWorld.Request(course));

        Assert.Equal(JournalExportOutcome.Exported, result.Outcome);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.File);
        Assert.Equal(FakeReportRenderer.Bytes, result.File.Content);
        Assert.Equal("Test Course One 2026-09-01–2026-09-30.xlsx", result.File.FileName);
        Assert.Single(world.Renderer.Rendered);
    }

    /// <summary>
    /// FR-003, AC-001: the workbook is the report page's own report for the same inputs, mapped once — nothing is
    /// recomputed. Compared structurally (records hold lists, so record equality would compare references).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("email")]
    public async Task TheWorkbook_IsTheMappedReportOfThePage_ForTheSameInputs(string? names)
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse();
        var ct = TestContext.Current.CancellationToken;

        await world.RunAsync(JournalExportWorld.Request(course, names: names));
        var page = await world.Report.Report.ExecuteAsync(
            new ReportRequest(
                [ReportTemplateTestData.BuiltInKey],
                [course.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                [JournalTestData.Period.FromText],
                [JournalTestData.Period.ToText],
                names is null ? null : [names]),
            JournalExportWorld.Uk,
            ct);

        var expected = ReportWorkbookMapper.Map(page.Page.Report!, new FakeReportTexts(), JournalExportWorld.Uk, PageOrientation.Portrait);
        var actual = Assert.Single(world.Renderer.Rendered);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    /// <summary>OD-004 a, VR-001: absent or empty orientation is portrait; landscape when asked.</summary>
    [Theory]
    [InlineData(null, PageOrientation.Portrait)]
    [InlineData("", PageOrientation.Portrait)]
    [InlineData("portrait", PageOrientation.Portrait)]
    [InlineData("landscape", PageOrientation.Landscape)]
    public async Task TheOrientation_IsTheRequestedOne_PortraitByDefault(string? orientation, PageOrientation expected)
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse();

        await world.RunAsync(JournalExportWorld.Request(course, orientation: orientation));

        Assert.Equal(expected, Assert.Single(world.Renderer.Rendered).Orientation);
    }

    /// <summary>AC-007, FR-010, VR-002: one row — actor from the session, the course, the period, the built-in marker, the rows, the format.</summary>
    [Fact]
    public async Task ASuccessfulExport_WritesOneAuditRow_WithTheDetailsAndNoContent()
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse();

        await world.RunAsync(JournalExportWorld.Request(course));

        var row = Assert.Single(world.Report.Audit.Written);
        Assert.Equal(AuditAction.JournalExported, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(ReportTemplateWorld.DeanId, row.ActorId);
        Assert.Equal(AppRole.Dean, row.ActorRole);
        Assert.Equal(AuditTargetType.Course, row.TargetType);
        Assert.Equal(course, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal("test-request-1", row.RequestId);
        Assert.Equal(JournalTestData.Period.From, row.ExportPeriodFrom);
        Assert.Equal(JournalTestData.Period.To, row.ExportPeriodTo);
        Assert.Null(row.ExportTemplateId);
        Assert.True(row.ExportTemplateBuiltIn);
        Assert.Equal(2, row.ExportRows);
        Assert.Equal(ExportFormat.Xlsx, row.ExportFormat);
        Assert.Equal(world.Report.Time.GetUtcNow(), row.OccurredAt);
    }

    /// <summary>FR-010, api-design §2.8: the row is committed once, as the declared BR-026 audit write, before the file is returned.</summary>
    [Fact]
    public async Task TheAuditRow_IsCommittedOnce_AsTheDeclaredAuditWrite()
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse();

        await world.RunAsync(JournalExportWorld.Request(course));

        Assert.Equal([PermittedServiceWrite.AuditEvent], world.Work.Declared);
        Assert.Equal(1, world.Report.Audit.WrittenAtLastCommit);
    }

    /// <summary>AC-007, db-design D-3: a created template is recorded by its id, not as the built-in.</summary>
    [Fact]
    public async Task ACreatedTemplate_IsRecordedByItsId()
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse();
        var template = world.Report.SeedTemplate();

        await world.RunAsync(JournalExportWorld.Request(course, template: template.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), ReportTemplateWorld.AdminId, AppRole.Admin);

        var row = Assert.Single(world.Report.Audit.Written);
        Assert.Equal(template.Id, row.ExportTemplateId);
        Assert.False(row.ExportTemplateBuiltIn);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Equal(ReportTemplateWorld.AdminId, row.ActorId);
    }

    /// <summary>FR-004.6, I-9: an empty report is exported and audited like any other, with zero rows.</summary>
    [Fact]
    public async Task AnEmptyReport_IsExported_AndAuditedWithZeroRows()
    {
        var world = new JournalExportWorld();
        var course = world.Report.Fields.AddCourse();

        var result = await world.RunAsync(JournalExportWorld.Request(course));

        Assert.Equal(JournalExportOutcome.Exported, result.Outcome);
        Assert.Equal(0, Assert.Single(world.Report.Audit.Written).ExportRows);
    }

    /// <summary>AC-006, FR-009, BR-026: read-only mode exports and audits; the guard is never consulted.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheExportWorks_AndIsAudited_WithoutAskingTheGuard()
    {
        var world = new JournalExportWorld(readOnly: true);
        var course = world.SeedCourse();

        var result = await world.RunAsync(JournalExportWorld.Request(course));

        Assert.Equal(JournalExportOutcome.Exported, result.Outcome);
        Assert.Single(world.Report.Audit.Written);
        Assert.Equal([PermittedServiceWrite.AuditEvent], world.Work.Declared);
        Assert.Empty(world.Report.ReadOnly.Operations);
    }

    public static TheoryData<JournalExportRequest, ExportField, ExportMessageKey> SingleFieldFailures(long course) => new()
    {
        { JournalExportWorld.Request(null), ExportField.CourseId, ExportMessageKey.CourseMalformed },
        { JournalExportWorld.Request(course) with { CourseId = "" }, ExportField.CourseId, ExportMessageKey.CourseMalformed },
        { JournalExportWorld.Request(course) with { CourseId = "4x2" }, ExportField.CourseId, ExportMessageKey.CourseMalformed },
        { JournalExportWorld.Request(course, from: null), ExportField.From, ExportMessageKey.FromMalformed },
        { JournalExportWorld.Request(course, from: "2026-9-1"), ExportField.From, ExportMessageKey.FromMalformed },
        { JournalExportWorld.Request(course, to: null), ExportField.To, ExportMessageKey.ToMalformed },
        { JournalExportWorld.Request(course, to: "2026-13-01"), ExportField.To, ExportMessageKey.ToMalformed },
        { JournalExportWorld.Request(course, from: "2026-09-30", to: "2026-09-01"), ExportField.To, ExportMessageKey.PeriodInverted },
        { JournalExportWorld.Request(course, template: "Academic-Journal"), ExportField.Template, ExportMessageKey.TemplateMalformed },
        { JournalExportWorld.Request(course, template: "-5"), ExportField.Template, ExportMessageKey.TemplateMalformed },
        { JournalExportWorld.Request(course, names: "Email"), ExportField.Names, ExportMessageKey.NameSourceMalformed },
        { JournalExportWorld.Request(course, orientation: "Landscape"), ExportField.Orientation, ExportMessageKey.OrientationInvalid },
        { JournalExportWorld.Request(course, orientation: " portrait"), ExportField.Orientation, ExportMessageKey.OrientationInvalid },
        { JournalExportWorld.Request(course, orientation: "album"), ExportField.Orientation, ExportMessageKey.OrientationInvalid },
    };

    /// <summary>AC-009, FR-005, VR-001: each malformed input is named; nothing is read, rendered, audited or committed.</summary>
    [Theory]
    [MemberData(nameof(SingleFieldFailures), 1L)]
    public async Task AMalformedInput_IsRefused_BeforeAnythingIsRead(JournalExportRequest request, ExportField field, ExportMessageKey key)
    {
        var world = new JournalExportWorld();
        world.SeedCourse();

        var result = await world.RunAsync(request);

        Assert.Equal(JournalExportOutcome.Invalid, result.Outcome);
        Assert.Null(result.File);
        Assert.Equal([new ExportFieldError(field, key)], result.Errors);
        Assert.Empty(world.Report.Fields.Calls);
        Assert.Empty(world.Report.Templates.Calls);
        AssertNothingHappened(world);
    }

    /// <summary>api-design §2.4: every failing field is listed, in the order template, course, from, to, names, orientation.</summary>
    [Fact]
    public async Task SeveralMalformedInputs_AreAllListed_InTheContractOrder()
    {
        var world = new JournalExportWorld();

        var result = await world.RunAsync(new JournalExportRequest("bad", null, "x", "y", "nobody", "sideways"));

        Assert.Equal(JournalExportOutcome.Invalid, result.Outcome);
        Assert.Equal(
            [
                new ExportFieldError(ExportField.Template, ExportMessageKey.TemplateMalformed),
                new ExportFieldError(ExportField.CourseId, ExportMessageKey.CourseMalformed),
                new ExportFieldError(ExportField.From, ExportMessageKey.FromMalformed),
                new ExportFieldError(ExportField.To, ExportMessageKey.ToMalformed),
                new ExportFieldError(ExportField.Names, ExportMessageKey.NameSourceMalformed),
                new ExportFieldError(ExportField.Orientation, ExportMessageKey.OrientationInvalid),
            ],
            result.Errors);
        AssertNothingHappened(world);
    }

    /// <summary>AC-009, FR-005: an unknown course or template is not found — both named when both are missing, template first.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AnUnknownCourseOrTemplate_IsNotFound_AndNothingIsExported(bool unknownTemplate, bool unknownCourse)
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse();
        var request = JournalExportWorld.Request(
            unknownCourse ? 987654 : course,
            template: unknownTemplate ? "424242" : ReportTemplateTestData.BuiltInKey);

        var result = await world.RunAsync(request);

        Assert.Equal(JournalExportOutcome.NotFound, result.Outcome);
        Assert.Null(result.File);
        var expected = new List<ExportFieldError>();
        if (unknownTemplate)
        {
            expected.Add(new ExportFieldError(ExportField.Template, ExportMessageKey.TemplateNotFound));
        }

        if (unknownCourse)
        {
            expected.Add(new ExportFieldError(ExportField.CourseId, ExportMessageKey.CourseUnknown));
        }

        Assert.Equal(expected, result.Errors);
        AssertNothingHappened(world);
    }

    /// <summary>FR-010, api-design §2.8: a failed render leaves no audit row — an export is never audited without its file.</summary>
    [Fact]
    public async Task AFailedRender_LeavesNoAuditRow()
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse();
        world.Renderer.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.RunAsync(JournalExportWorld.Request(course)));

        Assert.Empty(world.Report.Audit.Written);
        Assert.Empty(world.Work.Declared);
    }

    /// <summary>FR-007, VR-002: the program text comes from the text port; nothing of the request decides the language.</summary>
    [Fact]
    public async Task ProgramText_IsTakenFromTheTextPort()
    {
        var world = new JournalExportWorld();
        var course = world.SeedCourse();

        await world.RunAsync(JournalExportWorld.Request(course));

        var workbook = Assert.Single(world.Renderer.Rendered);
        Assert.Equal(
            [FakeReportTexts.Of(ReportText.SheetGrading), FakeReportTexts.Of(ReportText.SheetLessonTopics)],
            workbook.Sheets.Select(s => s.Name));
    }
}
