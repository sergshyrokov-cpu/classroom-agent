using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The read-only guard of the three template writes and the refused audit row it leaves (US-027 spec FR-013, FR-016;
/// api-design §2.6). The row's target is the reference only when it parses as a created-template id.
/// </summary>
internal static class ReportTemplateRefusalAudit
{
    public static async Task EnsureAllowedAsync(
        IReadOnlyModeGuard readOnlyMode,
        IAuditEventRepository auditEvents,
        IUnitOfWork unitOfWork,
        ServiceWriteScope writeScope,
        TimeProvider timeProvider,
        string operation,
        AuditAction action,
        long actorId,
        AppRole actorRole,
        string? templateRef,
        string? requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            await readOnlyMode.EnsureAllowedAsync(operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            long? target = ReportTemplateReference.Parse(templateRef, out var id) == ReportTemplateReference.Kind.Created
                ? id
                : null;
            auditEvents.Add(AuditEvent.ReportTemplateWriteRefused(
                actorId,
                actorRole,
                action,
                target,
                AuditRefusalCategory.ReadOnlyMode,
                timeProvider.GetUtcNow(),
                requestId));
            using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            throw;
        }
    }
}
