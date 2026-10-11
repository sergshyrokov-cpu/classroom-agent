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
/// US-044 spec FR-001, FR-003, FR-006, FR-013 (entity model §2.2): exports report 4 of one meeting to an Excel file
/// built from <see cref="GetMeetReportMeetingQuery"/>, with the delay line of <see cref="GetLastSynchronizationQuery"/>.
/// </summary>
/// <remarks>
/// No <see cref="IReadOnlyModeGuard"/>: the export works in read-only mode (BR-026); its only write, the audit row, is
/// declared as <see cref="PermittedServiceWrite.AuditEvent"/>. No Google port is in the graph. The meeting export takes
/// no period (spec I-10).
/// </remarks>
public sealed class ExportMeetMeetingCommand(
    GetMeetReportMeetingQuery meeting,
    GetLastSynchronizationQuery lastSynchronization,
    SchoolTimeZone schoolTimeZone,
    TimeProvider timeProvider,
    IReportTexts texts,
    IReportRenderer renderer,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope)
{
    public async Task<MeetExportResult> ExecuteAsync(
        MeetMeetingExportRequest request,
        long actorId,
        AppRole actorRole,
        CultureInfo uiCulture,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(uiCulture);

        // Spec VR-002, VR-003: course and meeting required, each a positive 64-bit id — no read yet.
        var errors = new List<MeetExportFieldError>();
        var courseId = request.CourseId is { } courseText ? ParseCourseId(courseText) : null;
        if (courseId is null)
        {
            errors.Add(new MeetExportFieldError(MeetExportField.CourseId, MeetExportMessageKey.CourseMalformed));
        }

        var meetingId = request.MeetingId is { } meetingText ? ParseCourseId(meetingText) : null;
        if (meetingId is null)
        {
            errors.Add(new MeetExportFieldError(MeetExportField.MeetingId, MeetExportMessageKey.MeetingMalformed));
        }

        if (!MeetExportLayout.TryReadOrientation(request.Orientation, out var orientation))
        {
            errors.Add(new MeetExportFieldError(MeetExportField.Orientation, MeetExportMessageKey.OrientationInvalid));
        }

        if (errors.Count > 0)
        {
            return new MeetExportResult(MeetExportOutcome.Invalid, null, errors);
        }

        // Spec FR-003, api-design §2.4: the page's own query; an unknown course, an unknown meeting and a meeting of
        // another course are one and the same "not found".
        var page = await meeting.ExecuteAsync(
            new MeetReportMeetingRequest(request.MeetingId!, [request.CourseId], [], []),
            uiCulture,
            cancellationToken);
        if (page.Outcome != MeetReportMeetingOutcome.Shown || page.Page.Meeting is not { } detail)
        {
            return new MeetExportResult(
                MeetExportOutcome.NotFound,
                null,
                [new MeetExportFieldError(MeetExportField.MeetingId, MeetExportMessageKey.MeetingNotFound)]);
        }

        var watermark = (await lastSynchronization.ExecuteAsync(cancellationToken)).MeetLoadedUpTo;

        // Spec FR-004, FR-006 … FR-008: rendered in memory; nothing is written to disk.
        var exportedAt = timeProvider.GetUtcNow();
        var workbook = MeetMeetingWorkbookMapper.Map(
            detail,
            watermark,
            TimeZoneInfo.ConvertTime(exportedAt, schoolTimeZone.Zone).DateTime,
            texts,
            uiCulture,
            orientation);
        var content = await renderer.RenderAsync(workbook, cancellationToken);
        var fileName = MeetExportFileName.BuildForMeeting(
            detail.CourseName, detail.StartedAt, texts.Get(ReportText.CourseFallback));

        // Spec FR-013, I-9: the identified people of the table, never the "Other participants" line.
        auditEvents.Add(AuditEvent.MeetMeetingExported(
            actorId,
            actorRole,
            courseId!.Value,
            meetingId!.Value,
            detail.People.Count,
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
                courseId.Value,
                null,
                null,
                meetingId.Value,
                detail.People.Count,
                null,
                MeetExportLayout.OrientationCode(orientation)));
    }
}
