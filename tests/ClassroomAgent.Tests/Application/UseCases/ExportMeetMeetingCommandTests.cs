using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-044 <c>ExportMeetMeetingCommand</c> over substituted ports (TC-1; spec FR-001, FR-003, FR-006, FR-009, FR-013;
/// entity model §2.2): validation before any read, not-found that does not tell a foreign meeting from a missing one,
/// the one-sheet workbook of report 4, the file and its name, and the one audit row.
/// </summary>
public sealed class ExportMeetMeetingCommandTests
{
    private static void AssertNothingHappened(MeetExportWorld w)
    {
        Assert.Empty(w.Renderer.Rendered);
        Assert.Empty(w.Audit.Written);
        Assert.Empty(w.Work.Declared);
    }

    /// <summary>AC-003, AC-008: a valid request answers the rendered bytes under the FR-008.1 meeting name.</summary>
    [Fact]
    public async Task AValidRequest_ExportsTheRenderedFile_UnderTheMeetCourseAndStartName()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        var result = await w.ExportMeetingAsync(
            MeetExportWorld.MeetingRequest(seeded.CourseId, MeetExportWorld.Id(seeded.MeetingId)));

        Assert.Equal(MeetExportOutcome.Exported, result.Outcome);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.File);
        Assert.Equal(FakeReportRenderer.Bytes, result.File.Content);
        Assert.Equal(
            MeetExportFileName.BuildForMeeting(
                MeetExportWorld.CourseName,
                new DateTime(2026, 10, 5, 10, 0, 0),
                FakeReportTexts.Of(ReportText.CourseFallback)),
            result.File.FileName);
        Assert.Single(Assert.Single(w.Renderer.Rendered).Sheets);
    }

    /// <summary>AC-007, VR-003: absent orientation is landscape; portrait when asked.</summary>
    [Theory]
    [InlineData(null, PageOrientation.Landscape)]
    [InlineData("portrait", PageOrientation.Portrait)]
    public async Task TheOrientation_IsTheRequestedOne_LandscapeByDefault(string? orientation, PageOrientation expected)
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        await w.ExportMeetingAsync(
            MeetExportWorld.MeetingRequest(seeded.CourseId, MeetExportWorld.Id(seeded.MeetingId), orientation));

        Assert.Equal(expected, Assert.Single(w.Renderer.Rendered).Orientation);
    }

    /// <summary>AC-010, FR-013: one row — actor, course, meeting id, identified rows; no period, no meeting count.</summary>
    [Fact]
    public async Task ASuccessfulExport_WritesOneAuditRow_WithTheMeetingIdAndNoContent()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        await w.ExportMeetingAsync(
            MeetExportWorld.MeetingRequest(seeded.CourseId, MeetExportWorld.Id(seeded.MeetingId)));

        var row = Assert.Single(w.Audit.Written);
        Assert.Equal(AuditAction.MeetMeetingExported, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(MeetExportWorld.DeanId, row.ActorId);
        Assert.Equal(AppRole.Dean, row.ActorRole);
        Assert.Equal(AuditTargetType.Course, row.TargetType);
        Assert.Equal(seeded.CourseId, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Equal("test-request-1", row.RequestId);
        Assert.Equal(seeded.MeetingId, row.ExportMeetingId);
        Assert.Equal(3, row.ExportRows);
        Assert.Null(row.ExportPeriodFrom);
        Assert.Null(row.ExportPeriodTo);
        Assert.Null(row.ExportMeetings);
        Assert.Null(row.MeetCode);
        Assert.Equal(ExportFormat.Xlsx, row.ExportFormat);
        Assert.Equal([PermittedServiceWrite.AuditEvent], w.Work.Declared);
    }

    /// <summary>AC-003, AC-005, I-6: the table, the empty row, then the "Other participants" line with no email.</summary>
    [Fact]
    public async Task TheSheet_HasTheTableThenAnEmptyRowThenTheOtherParticipantsLine()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        await w.ExportMeetingAsync(
            MeetExportWorld.MeetingRequest(seeded.CourseId, MeetExportWorld.Id(seeded.MeetingId)));

        var sheet = Assert.Single(Assert.Single(w.Renderer.Rendered).Sheets);
        Assert.True(sheet.AutoFilter);
        Assert.Equal(8, sheet.TableHeaderRowIndex);
        Assert.Equal(11, sheet.TableLastRowIndex);
        Assert.True(sheet.Rows.Count > 12);
        var last = sheet.TableLastRowIndex!.Value;
        Assert.All(sheet.Rows[last + 1].Cells, c => Assert.Equal(WorkbookCellKind.Empty, c.Kind));
        Assert.True(sheet.Rows.Count > last + 2);
        var numbers = sheet.Rows[last + 2].Cells.Where(c => c.Kind == WorkbookCellKind.Number).Select(c => c.Number).ToList();
        Assert.Contains(1m, numbers);
        Assert.Contains(5m, numbers);

        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            if (i is >= 9 and <= 11 or 4)
            {
                continue;
            }

            Assert.All(
                sheet.Rows[i].Cells,
                c => Assert.DoesNotContain('@', c.Text ?? string.Empty));
        }
    }

    /// <summary>I-6: when nobody is identified the table is its header row only, and zero rows are audited.</summary>
    [Fact]
    public async Task WhenNobodyIsIdentified_TheTableIsOnlyItsHeaderRow()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();
        var nobody = w.Source.AddMeeting(
            MeetExportWorld.LinkedCode,
            MeetReportsTestData.Email("teacher.one"),
            MeetReportsTestData.Period.Local(10, 12, 10),
            MeetReportsTestData.Period.Local(10, 12, 11),
            (null, 600));

        var result = await w.ExportMeetingAsync(
            MeetExportWorld.MeetingRequest(seeded.CourseId, MeetExportWorld.Id(nobody)));

        Assert.Equal(MeetExportOutcome.Exported, result.Outcome);
        var sheet = Assert.Single(Assert.Single(w.Renderer.Rendered).Sheets);
        Assert.Equal(sheet.TableHeaderRowIndex, sheet.TableLastRowIndex);
        Assert.Equal(0, Assert.Single(w.Audit.Written).ExportRows);
    }

    /// <summary>AC-012, FR-009: each malformed member is named; nothing is read, rendered, audited or committed.</summary>
    [Theory]
    [InlineData(null, "1", MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed)]
    [InlineData("abc", "1", MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed)]
    [InlineData("1", null, MeetExportField.MeetingId, MeetExportMessageKey.MeetingMalformed)]
    [InlineData("1", "", MeetExportField.MeetingId, MeetExportMessageKey.MeetingMalformed)]
    [InlineData("1", "abc", MeetExportField.MeetingId, MeetExportMessageKey.MeetingMalformed)]
    [InlineData("1", "0", MeetExportField.MeetingId, MeetExportMessageKey.MeetingMalformed)]
    [InlineData("1", "-5", MeetExportField.MeetingId, MeetExportMessageKey.MeetingMalformed)]
    [InlineData("1", "1.5", MeetExportField.MeetingId, MeetExportMessageKey.MeetingMalformed)]
    [InlineData("1", "99999999999999999999", MeetExportField.MeetingId, MeetExportMessageKey.MeetingMalformed)]
    public async Task AMalformedInput_IsRefused_BeforeAnythingIsRead(
        string? courseId,
        string? meetingId,
        MeetExportField field,
        MeetExportMessageKey key)
    {
        var w = new MeetExportWorld();
        w.Seed();

        var result = await w.ExportMeetingAsync(
            new MeetMeetingExportRequest(courseId, meetingId, null));

        Assert.Equal(MeetExportOutcome.Invalid, result.Outcome);
        Assert.Null(result.File);
        Assert.Contains(new MeetExportFieldError(field, key), result.Errors);
        Assert.Equal(0, w.Reads);
        AssertNothingHappened(w);
    }

    /// <summary>AC-007, AC-012: an orientation other than the two values is refused before any read.</summary>
    [Fact]
    public async Task AnInvalidOrientation_IsRefused_BeforeAnythingIsRead()
    {
        var w = new MeetExportWorld();
        w.Seed();

        var result = await w.ExportMeetingAsync(MeetExportWorld.MeetingRequest(1, "1", "x"));

        Assert.Equal(MeetExportOutcome.Invalid, result.Outcome);
        Assert.Null(result.File);
        Assert.Contains(new MeetExportFieldError(MeetExportField.Orientation, MeetExportMessageKey.OrientationInvalid), result.Errors);
        Assert.Equal(0, w.Reads);
        AssertNothingHappened(w);
    }

    /// <summary>AC-012: an unknown meeting is not found, named on the meeting id; nothing is exported.</summary>
    [Fact]
    public async Task AnUnknownMeeting_IsNotFound_AndNothingIsExported()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        var result = await w.ExportMeetingAsync(MeetExportWorld.MeetingRequest(seeded.CourseId, "999999"));

        Assert.Equal(MeetExportOutcome.NotFound, result.Outcome);
        Assert.Null(result.File);
        Assert.Contains(new MeetExportFieldError(MeetExportField.MeetingId, MeetExportMessageKey.MeetingNotFound), result.Errors);
        AssertNothingHappened(w);
    }

    /// <summary>AC-012: a meeting of another course answers exactly as an unknown one — it does not reveal the meeting.</summary>
    [Fact]
    public async Task AMeetingOfAnotherCourse_IsNotFound_ExactlyAsAnUnknownOne()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();
        var unknown = await w.ExportMeetingAsync(MeetExportWorld.MeetingRequest(seeded.CourseId, "999999"));

        var result = await w.ExportMeetingAsync(
            MeetExportWorld.MeetingRequest(seeded.CourseId, MeetExportWorld.Id(seeded.OtherCourseMeetingId)));

        Assert.Equal(MeetExportOutcome.NotFound, result.Outcome);
        Assert.Null(result.File);
        Assert.Equal(unknown.Errors, result.Errors);
        AssertNothingHappened(w);
    }

    /// <summary>AC-012: an unknown course with a real meeting id is not found as well.</summary>
    [Fact]
    public async Task AnUnknownCourse_IsNotFound_AndNothingIsExported()
    {
        var w = new MeetExportWorld();
        var seeded = w.Seed();

        var result = await w.ExportMeetingAsync(
            MeetExportWorld.MeetingRequest(999999, MeetExportWorld.Id(seeded.MeetingId)));

        Assert.Equal(MeetExportOutcome.NotFound, result.Outcome);
        Assert.Null(result.File);
        AssertNothingHappened(w);
    }
}
