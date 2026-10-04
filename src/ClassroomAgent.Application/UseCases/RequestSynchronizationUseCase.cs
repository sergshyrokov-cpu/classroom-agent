using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// "Синхронизировать" (US-019 spec FR-001): read-only guard first, then the connection state (OD-009 a), then the
/// request through <see cref="ISynchronizationRequests"/>, then one audit row. It makes no Google call and reads no
/// secret; the run it requests keeps its own checks (US-013 FR-007).
/// </summary>
/// <remarks>
/// The request is enqueued before the audit row is committed (spec FR-001 steps 3–4): an in-process request cannot be
/// enlisted in the database transaction, so a failed commit after it leaves the run requested and unaudited
/// (db-design §5).
/// </remarks>
public sealed class RequestSynchronizationUseCase(
    ISynchronizationRequests requests,
    GetWorkspaceConnectionQuery connectionQuery,
    IAuditEventRepository auditEvents,
    IReadOnlyModeGuard readOnlyMode,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only refusal carries (US-007 spec VR-001: a constant, never data).</summary>
    public const string Operation = "Synchronization.Request";

    public async Task<RequestSynchronizationOutcome> ExecuteAsync(
        long actorId,
        AppRole actorRole,
        string? requestId,
        CancellationToken cancellationToken)
    {
        // 1: read-only mode first (spec FR-001 step 1, FR-006) — nothing is read or enqueued before it.
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(actorId, actorRole, AuditRefusalCategory.ReadOnlyMode, requestId, cancellationToken);
            throw;
        }

        // 2: the US-009 state decides whether the connection may be used (OD-009 a; US-011 I-11).
        var connection = await connectionQuery.ExecuteAsync(cancellationToken);
        if (!connection.IsUsable)
        {
            await AuditRefusalAsync(actorId, actorRole, AuditRefusalCategory.ConnectionNotUsable, requestId, cancellationToken);
            return RequestSynchronizationOutcome.ConnectionNotUsable;
        }

        // 3: enqueue; the answer says whether other work was in progress at that moment (spec FR-003, I-1).
        var timing = await requests.RequestAsync(cancellationToken);

        // 4: the audit row — the request was accepted, not that a run happened (spec I-2).
        auditEvents.Add(AuditEvent.SynchronizationRequested(actorId, actorRole, timeProvider.GetUtcNow(), requestId));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return timing == SynchronizationRequestTiming.AfterCurrentWork
            ? RequestSynchronizationOutcome.RequestedAfterCurrentWork
            : RequestSynchronizationOutcome.Requested;
    }

    /// <summary>
    /// One audit row and nothing else, declared as <see cref="PermittedServiceWrite.AuditEvent"/> — on the BR-026
    /// closed list — so the row is written in read-only mode too (US-009 FR-008, US-011 FR-008).
    /// </summary>
    private async Task AuditRefusalAsync(
        long actorId,
        AppRole actorRole,
        AuditRefusalCategory category,
        string? requestId,
        CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.SynchronizationRequestRefused(
            actorId,
            actorRole,
            category,
            timeProvider.GetUtcNow(),
            requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
