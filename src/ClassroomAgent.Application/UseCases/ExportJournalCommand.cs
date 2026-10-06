using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>US-028 entity model §3.2: exports a course journal to a file. Skeleton (OD-005).</summary>
public sealed class ExportJournalCommand(
    IJournalFieldSource fields,
    IReportTemplateRepository templates,
    SchoolTimeZone schoolTimeZone,
    TimeProvider timeProvider,
    IReportTexts texts,
    IReportRenderer renderer,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope)
{
    public Task<JournalExportResult> ExecuteAsync(
        JournalExportRequest request,
        long actorId,
        AppRole actorRole,
        CultureInfo uiCulture,
        string? requestId,
        CancellationToken cancellationToken)
    {
        _ = (fields, templates, schoolTimeZone, timeProvider, texts, renderer, auditEvents, unitOfWork, writeScope);
        throw new NotImplementedException("US-028 IMPLEMENTATION");
    }
}
