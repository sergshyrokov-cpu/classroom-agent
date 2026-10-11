using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-044 spec FR-001, FR-003, FR-005, FR-013 (entity model §2.2): exports reports 1–3 of a course and period to an
/// Excel file built from <see cref="GetMeetReportsQuery"/>. Validate → build → map → render → audit → commit → return.
/// </summary>
/// <remarks>
/// No <see cref="IReadOnlyModeGuard"/>: the export works in read-only mode (BR-026); its only write, the audit row, is
/// declared as <see cref="PermittedServiceWrite.AuditEvent"/>. No Google port is in the graph. The actor, the role and
/// the UI language come from the session, never from the request (spec VR-004).
/// </remarks>
public sealed class ExportMeetReportsCommand(
    GetMeetReportsQuery reports,
    SchoolTimeZone schoolTimeZone,
    TimeProvider timeProvider,
    IReportTexts texts,
    IReportRenderer renderer,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope)
{
    public async Task<MeetExportResult> ExecuteAsync(
        MeetReportsExportRequest request,
        long actorId,
        AppRole actorRole,
        CultureInfo uiCulture,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(uiCulture);

        // Spec VR-001, VR-003: the page's rules with every member but the orientation required — no read yet.
        var errors = new List<MeetExportFieldError>();
        var courseId = request.CourseId is { } courseText ? ParseCourseId(courseText) : null;
        if (courseId is null)
        {
            errors.Add(new MeetExportFieldError(MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed));
        }

        var from = RequiredDate(request.From, MeetExportField.From, MeetExportMessageKey.FromMalformed, errors);
        var to = RequiredDate(request.To, MeetExportField.To, MeetExportMessageKey.ToMalformed, errors);
        if (from is { } f && to is { } t && f > t)
        {
            errors.Add(new MeetExportFieldError(MeetExportField.To, MeetExportMessageKey.PeriodInverted));
        }

        if (!MeetExportLayout.TryReadOrientation(request.Orientation, out var orientation))
        {
            errors.Add(new MeetExportFieldError(MeetExportField.Orientation, MeetExportMessageKey.OrientationInvalid));
        }

        if (errors.Count > 0)
        {
            return new MeetExportResult(MeetExportOutcome.Invalid, null, errors);
        }

        // Spec FR-003: the page's own query, with the same inputs.
        var page = await reports.ExecuteAsync(
            new MeetReportsRequest([request.CourseId], [request.From], [request.To]),
            uiCulture,
            cancellationToken);
        if (page.Outcome == MeetReportsPageOutcome.CourseUnknown)
        {
            return new MeetExportResult(
                MeetExportOutcome.NotFound,
                null,
                [new MeetExportFieldError(MeetExportField.CourseId, MeetExportMessageKey.CourseUnknown)]);
        }

        if (page.Outcome != MeetReportsPageOutcome.Shown || page.Page.Reports is not { } built)
        {
            throw new InvalidOperationException("Validated export inputs did not build the reports of the page.");
        }

        var course = courseId!.Value;
        var period = (From: from!.Value, To: to!.Value);
        var courseName = page.Page.Courses.First(c => c.Id == course).Name;

        // Spec FR-004 … FR-008: the workbook is rendered in memory; nothing is written to disk.
        var exportedAt = timeProvider.GetUtcNow();
        var workbook = MeetReportsWorkbookMapper.Map(
            courseName,
            period.From,
            period.To,
            built,
            TimeZoneInfo.ConvertTime(exportedAt, schoolTimeZone.Zone).DateTime,
            texts,
            uiCulture,
            orientation);
        var content = await renderer.RenderAsync(workbook, cancellationToken);
        var fileName = MeetExportFileName.BuildForPeriod(
            courseName, period.From, period.To, texts.Get(ReportText.CourseFallback));

        // Spec FR-013: the audit row is committed before the file is returned; an export is never delivered unaudited.
        auditEvents.Add(AuditEvent.MeetReportsExported(
            actorId,
            actorRole,
            course,
            period.From,
            period.To,
            built.Students.Count,
            built.Meetings.Count,
            ExportFormat.Xlsx,
            exportedAt,
            requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new MeetExportResult(
            MeetExportOutcome.Exported,
            new JournalExportFile(content, fileName),
            [],
            new MeetExportSummary(
                course,
                period.From,
                period.To,
                null,
                built.Students.Count,
                built.Meetings.Count,
                MeetExportLayout.OrientationCode(orientation)));
    }

    /// <summary>Spec VR-001: a required date; absent or malformed is the field's own message.</summary>
    private static DateOnly? RequiredDate(
        string? value, MeetExportField field, MeetExportMessageKey key, List<MeetExportFieldError> errors)
    {
        DateOnly? date = null;
        if (value is not null)
        {
            var read = ReadDate([value], default, out var malformed);
            date = malformed ? null : read;
        }

        if (date is null)
        {
            errors.Add(new MeetExportFieldError(field, key));
        }

        return date;
    }
}
