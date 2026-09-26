using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// "Проверить доступ" (US-011 spec FR-006): read-only guard first, then the connection state, then the eight steps,
/// then one audit row. Nothing is stored about what the check found (OD-003), and nothing limits how often it runs
/// (OD-005).
/// </summary>
/// <remarks>
/// The first use case holding a real <see cref="IGoogleDataPort"/>: it takes <see cref="IReadOnlyModeGuard"/> and
/// calls it before the port, the connection or anything else is touched, so in read-only mode not even a token
/// request leaves the process (BR-026, SC-5; the carried US-007 finding F-5). The read-only refusal is audited and
/// rethrown for the host's <c>409</c>, exactly as US-009 FR-008 established.
/// </remarks>
public sealed class RunAccessCheckUseCase(
    IGoogleAccessProbe probe,
    GetWorkspaceConnectionQuery connectionQuery,
    IWorkspaceConnectionRepository connections,
    IAuditEventRepository auditEvents,
    IReadOnlyModeGuard readOnlyMode,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only refusal carries (US-007 spec VR-001: a constant, never data).</summary>
    public const string Operation = "AccessCheck.Run";

    public async Task<RunAccessCheckOutcome> ExecuteAsync(
        long actorId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        // 1: read-only mode first (spec FR-006 step 1, FR-009).
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            var stored = await connections.GetForReadAsync(cancellationToken);
            await AuditRefusalAsync(actorId, AuditRefusalCategory.ReadOnlyMode, stored?.Id, requestId, cancellationToken);
            throw;
        }

        // 2: the US-009 state decides whether the connection may be used (spec FR-006 step 2, I-11).
        var view = await connectionQuery.ExecuteAsync(cancellationToken);
        var connection = await connections.GetForReadAsync(cancellationToken);
        if (!view.IsUsable || connection is null || view.SavedImpersonationUserEmail is not { } technicalAccount)
        {
            await AuditRefusalAsync(actorId, AuditRefusalCategory.ConnectionNotUsable, connection?.Id, requestId, cancellationToken);
            return RunAccessCheckOutcome.Refused(view.State == WorkspaceConnectionState.DomainMismatch
                ? AccessCheckRefusal.DomainMismatch
                : AccessCheckRefusal.NotConfigured);
        }

        // 3: the eight steps, bounded by the time limit (spec I-2).
        var result = await AccessCheckSteps.RunAsync(probe, technicalAccount, timeProvider, cancellationToken);

        // 4: the audit row — the check was carried out, whatever it found (spec I-4).
        auditEvents.Add(AuditEvent.AccessCheckRun(actorId, connection.Id, timeProvider.GetUtcNow(), requestId));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RunAccessCheckOutcome.Ran(result);
    }

    /// <summary>
    /// One audit row and nothing else, declared as <see cref="PermittedServiceWrite.AuditEvent"/> — on the BR-026
    /// closed list — so the row is written in read-only mode too (US-009 FR-008; db-design §3.3).
    /// </summary>
    private async Task AuditRefusalAsync(
        long actorId,
        AuditRefusalCategory category,
        long? connectionId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.AccessCheckRefused(actorId, category, connectionId, timeProvider.GetUtcNow(), requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
