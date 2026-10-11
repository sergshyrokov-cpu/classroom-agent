using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-044 <c>ExportMeetReportsCommand</c> over substituted ports (TC-1; spec FR-001, FR-003, FR-005, FR-007, FR-009,
/// FR-013; entity model §2.2): validation before any read, not-found, the workbook model, the file and its name, the
/// one audit row committed as the declared BR-026 write, and no row without a file.
/// </summary>
public sealed class ExportMeetReportsCommandTests
{
    private const string ValidFrom = "2026-10-01";

    private const string ValidTo = "2026-10-31";

    private static void AssertNothingHappened(MeetExportWorld w)
    {
        Assert.Empty(w.Renderer.Rendered);
        Assert.Empty(w.Audit.Written);
        Assert.Empty(w.Work.Declared);
    }

    /// <summary>AC-002, AC-008: a valid request answers the rendered bytes under the FR-008.1 name.</summary>
    [Fact]
    public async Task AValidRequest_ExportsTheRenderedFile_UnderTheMeetCourseAndPeriodName()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        var result = await w.ExportReportsAsync(MeetExportWorld.ReportsRequest(seeded.CourseId));

        Assert.Equal(MeetExportOutcome.Exported, result.Outcome);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.File);
        Assert.Equal(FakeReportRenderer.Bytes, result.File.Content);
        Assert.Equal(
            MeetExportFileName.BuildForPeriod(
                MeetExportWorld.CourseName,
                MeetReportsTestData.Period.From,
                MeetReportsTestData.Period.To,
                FakeReportTexts.Of(ReportText.CourseFallback)),
            result.File.FileName);
        Assert.Single(w.Renderer.Rendered);
    }

    /// <summary>AC-010, FR-013: one row — actor, course, period, student rows, meeting rows, format, no meeting id, no template.</summary>
    [Theory]
    [InlineData(AppRole.Dean)]
    [InlineData(AppRole.Admin)]
    public async Task ASuccessfulExport_WritesOneAuditRow_WithTheCountsAndNoContent(AppRole role)
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        await w.ExportReportsAsync(MeetExportWorld.ReportsRequest(seeded.CourseId), role);

        var row = Assert.Single(w.Audit.Written);
        Assert.Equal(AuditAction.MeetReportsExported, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(MeetExportWorld.DeanId, row.ActorId);
        Assert.Equal(role, row.ActorRole);
        Assert.Equal(AuditTargetType.Course, row.TargetType);
        Assert.Equal(seeded.CourseId, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal("test-request-1", row.RequestId);
        Assert.Equal(MeetReportsTestData.Period.From, row.ExportPeriodFrom);
        Assert.Equal(MeetReportsTestData.Period.To, row.ExportPeriodTo);
        Assert.Equal(2, row.ExportRows);
        Assert.Equal(1, row.ExportMeetings);
        Assert.Equal(ExportFormat.Xlsx, row.ExportFormat);
        Assert.Null(row.ExportMeetingId);
        Assert.Null(row.ExportTemplateId);
        Assert.Null(row.ExportTemplateBuiltIn);
        Assert.Null(row.MeetCode);
        Assert.Equal(MeetReportsTestData.Now, row.OccurredAt);
    }

    /// <summary>FR-013, BR-026: the row is committed once, as the declared audit write.</summary>
    [Fact]
    public async Task TheAuditRow_IsCommittedOnce_AsTheDeclaredAuditWrite()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        await w.ExportReportsAsync(MeetExportWorld.ReportsRequest(seeded.CourseId));

        Assert.Equal([PermittedServiceWrite.AuditEvent], w.Work.Declared);
        Assert.Equal([1], w.Work.StagedAuditRows);
    }

    /// <summary>AC-002, AC-004, AC-006, AC-007: three sheets, typed cells, table header on row 6, autofilter, formats.</summary>
    [Fact]
    public async Task TheWorkbook_HasThreeSheets_WithTypedCellsAndTheTableLayout()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        await w.ExportReportsAsync(MeetExportWorld.ReportsRequest(seeded.CourseId));

        var workbook = Assert.Single(w.Renderer.Rendered);
        Assert.Equal(3, workbook.Sheets.Count);
        Assert.NotNull(workbook.TimeFormat);
        Assert.NotNull(workbook.DateTimeFormat);
        foreach (var sheet in workbook.Sheets)
        {
            Assert.Equal(5, sheet.TableHeaderRowIndex);
            Assert.True(sheet.AutoFilter);
            Assert.Equal(WorkbookCellKind.DateTime, sheet.Rows[2].Cells[1].Kind);
        }

        var summary = workbook.Sheets[0];
        for (var i = 6; i <= 9; i++)
        {
            Assert.Equal(WorkbookCellKind.Number, summary.Rows[i].Cells[1].Kind);
        }

        Assert.Equal("0.0", summary.Rows[9].Cells[1].NumberFormat);

        var meeting = workbook.Sheets[1].Rows[6].Cells;
        Assert.Equal(WorkbookCellKind.Date, meeting[0].Kind);
        Assert.Equal(WorkbookCellKind.Time, meeting[1].Kind);
        Assert.Equal(WorkbookCellKind.Time, meeting[2].Kind);
        Assert.Equal(WorkbookCellKind.Number, meeting[3].Kind);
    }

    /// <summary>AC-007, VR-003: absent orientation is landscape; the two values are accepted as written.</summary>
    [Theory]
    [InlineData(null, PageOrientation.Landscape)]
    [InlineData("landscape", PageOrientation.Landscape)]
    [InlineData("portrait", PageOrientation.Portrait)]
    public async Task TheOrientation_IsTheRequestedOne_LandscapeByDefault(string? orientation, PageOrientation expected)
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        await w.ExportReportsAsync(MeetExportWorld.ReportsRequest(seeded.CourseId, orientation: orientation));

        Assert.Equal(expected, Assert.Single(w.Renderer.Rendered).Orientation);
    }

    /// <summary>AC-005, I-9: a course without meetings is exported; its empty table has no autofilter; counts are zero.</summary>
    [Fact]
    public async Task ACourseWithNoMeetings_IsExported_WithoutAnAutoFilter_AndAuditedWithZeroCounts()
    {
        var w = new MeetExportWorld();
        w.Seed();
        var empty = w.Source.AddCourse("Test Course Empty");

        var result = await w.ExportReportsAsync(MeetExportWorld.ReportsRequest(empty));

        Assert.Equal(MeetExportOutcome.Exported, result.Outcome);
        var workbook = Assert.Single(w.Renderer.Rendered);
        Assert.False(workbook.Sheets[1].AutoFilter);
        var row = Assert.Single(w.Audit.Written);
        Assert.Equal(0, row.ExportMeetings);
        Assert.Equal(0, row.ExportRows);
    }

    /// <summary>AC-012, VR-001: a one-day period is valid.</summary>
    [Fact]
    public async Task AOneDayPeriod_IsExported()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        var result = await w.ExportReportsAsync(
            MeetExportWorld.ReportsRequest(seeded.CourseId, from: "2026-10-05", to: "2026-10-05"));

        Assert.Equal(MeetExportOutcome.Exported, result.Outcome);
    }

    /// <summary>AC-012, FR-009: each malformed member is named; nothing is read, rendered, audited or committed.</summary>
    [Theory]
    [InlineData(null, ValidFrom, ValidTo, null, MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed)]
    [InlineData("abc", ValidFrom, ValidTo, null, MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed)]
    [InlineData("0", ValidFrom, ValidTo, null, MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed)]
    [InlineData("-1", ValidFrom, ValidTo, null, MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed)]
    [InlineData("1 ", ValidFrom, ValidTo, null, MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed)]
    [InlineData("99999999999999999999", ValidFrom, ValidTo, null, MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed)]
    [InlineData("1", null, ValidTo, null, MeetExportField.From, MeetExportMessageKey.FromMalformed)]
    [InlineData("1", "2026-13-01", ValidTo, null, MeetExportField.From, MeetExportMessageKey.FromMalformed)]
    [InlineData("1", "1999-12-31", ValidTo, null, MeetExportField.From, MeetExportMessageKey.FromMalformed)]
    [InlineData("1", ValidFrom, null, null, MeetExportField.To, MeetExportMessageKey.ToMalformed)]
    [InlineData("1", ValidFrom, "2101-01-01", null, MeetExportField.To, MeetExportMessageKey.ToMalformed)]
    [InlineData("1", ValidFrom, ValidTo, "Portrait", MeetExportField.Orientation, MeetExportMessageKey.OrientationInvalid)]
    [InlineData("1", ValidFrom, ValidTo, "LANDSCAPE", MeetExportField.Orientation, MeetExportMessageKey.OrientationInvalid)]
    [InlineData("1", ValidFrom, ValidTo, "x", MeetExportField.Orientation, MeetExportMessageKey.OrientationInvalid)]
    [InlineData("1", ValidFrom, ValidTo, " portrait", MeetExportField.Orientation, MeetExportMessageKey.OrientationInvalid)]
    public async Task AMalformedInput_IsRefused_BeforeAnythingIsRead(
        string? courseId,
        string? from,
        string? to,
        string? orientation,
        MeetExportField field,
        MeetExportMessageKey key)
    {
        var w = new MeetExportWorld();
        w.Seed();

        var result = await w.ExportReportsAsync(new MeetReportsExportRequest(courseId, from, to, orientation));

        Assert.Equal(MeetExportOutcome.Invalid, result.Outcome);
        Assert.Null(result.File);
        Assert.Contains(new MeetExportFieldError(field, key), result.Errors);
        Assert.Equal(0, w.Reads);
        AssertNothingHappened(w);
    }

    /// <summary>AC-012: an inverted period is refused with its own message, on either date.</summary>
    [Fact]
    public async Task AnInvertedPeriod_IsRefused_BeforeAnythingIsRead()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        var result = await w.ExportReportsAsync(
            MeetExportWorld.ReportsRequest(seeded.CourseId, from: "2026-10-31", to: "2026-10-01"));

        Assert.Equal(MeetExportOutcome.Invalid, result.Outcome);
        Assert.Null(result.File);
        var error = Assert.Single(result.Errors, e => e.Key == MeetExportMessageKey.PeriodInverted);
        Assert.True(error.Field is MeetExportField.From or MeetExportField.To);
        Assert.Equal(0, w.Reads);
        AssertNothingHappened(w);
    }

    /// <summary>AC-012: every failing member is listed.</summary>
    [Fact]
    public async Task SeveralMalformedInputs_AreAllListed()
    {
        var w = new MeetExportWorld();
        w.Seed();

        var result = await w.ExportReportsAsync(new MeetReportsExportRequest("abc", ValidFrom, ValidTo, "x"));

        Assert.Equal(MeetExportOutcome.Invalid, result.Outcome);
        Assert.Contains(new MeetExportFieldError(MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed), result.Errors);
        Assert.Contains(new MeetExportFieldError(MeetExportField.Orientation, MeetExportMessageKey.OrientationInvalid), result.Errors);
        Assert.Equal(0, w.Reads);
        AssertNothingHappened(w);
    }

    /// <summary>AC-012: an unknown course is not found — no file, no audit row, no commit.</summary>
    [Fact]
    public async Task AnUnknownCourse_IsNotFound_AndNothingIsExported()
    {
        var w = new MeetExportWorld();
        w.Seed();

        var result = await w.ExportReportsAsync(MeetExportWorld.ReportsRequest(999999));

        Assert.Equal(MeetExportOutcome.NotFound, result.Outcome);
        Assert.Null(result.File);
        Assert.Contains(new MeetExportFieldError(MeetExportField.CourseId, MeetExportMessageKey.CourseUnknown), result.Errors);
        AssertNothingHappened(w);
    }

    /// <summary>FR-013: a failed render leaves no audit row — an export is never audited without its file.</summary>
    [Fact]
    public async Task AFailedRender_LeavesNoAuditRow()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();
        w.Renderer.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => w.ExportReportsAsync(MeetExportWorld.ReportsRequest(seeded.CourseId)));

        Assert.Empty(w.Audit.Written);
        Assert.Empty(w.Work.Declared);
    }

    /// <summary>FR-013: a failed commit returns no file — the exception reaches the caller.</summary>
    [Fact]
    public async Task AFailedCommit_ReturnsNoFile()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();
        w.Work.FailNextCommit = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => w.ExportReportsAsync(MeetExportWorld.ReportsRequest(seeded.CourseId)));
    }
}
