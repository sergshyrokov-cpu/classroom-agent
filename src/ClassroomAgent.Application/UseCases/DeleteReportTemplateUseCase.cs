using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>Deletes a created template (US-027 spec FR-010, FR-013, FR-016); order of api-design §2.6.</summary>
public sealed class DeleteReportTemplateUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IReportTemplateRepository templates,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    public const string Operation = "ReportTemplate.Delete";

    public async Task<ReportTemplateOutcome> ExecuteAsync(
        long actorId,
        AppRole actorRole,
        string? templateRef,
        string? requestId,
        CancellationToken cancellationToken)
    {
        await ReportTemplateRefusalAudit.EnsureAllowedAsync(
            readOnlyMode,
            auditEvents,
            unitOfWork,
            writeScope,
            timeProvider,
            Operation,
            AuditAction.ReportTemplateDeleted,
            actorId,
            actorRole,
            templateRef,
            requestId,
            cancellationToken);

        switch (ReportTemplateReference.Parse(templateRef, out var id))
        {
            case ReportTemplateReference.Kind.Malformed:
                return ReportTemplateOutcome.ReferenceMalformed;
            case ReportTemplateReference.Kind.BuiltIn:
                return ReportTemplateOutcome.BuiltInNotChangeable;
        }

        var template = await templates.GetAsync(id, true, cancellationToken);
        if (template is null)
        {
            return ReportTemplateOutcome.NotFound;
        }

        var now = timeProvider.GetUtcNow();
        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                templates.Remove(template);
                auditEvents.Add(AuditEvent.ReportTemplateWritten(
                    actorId,
                    actorRole,
                    AuditAction.ReportTemplateDeleted,
                    template.Id,
                    now,
                    requestId));
                await unitOfWork.SaveChangesAsync(token);
            },
            cancellationToken);

        return ReportTemplateOutcome.Succeeded;
    }
}
