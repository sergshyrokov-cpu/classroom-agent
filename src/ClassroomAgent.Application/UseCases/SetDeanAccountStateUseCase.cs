using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// An Admin disables or re-enables a Dean account (US-012 spec FR-007, FR-008). One use case for both
/// directions, as the contract has one operation; the audit row still names which it was (db-design §4.1).
/// </summary>
/// <remarks>US-012 TEST_WRITING skeleton (OD-005) — IMPLEMENTATION writes the body.</remarks>
public sealed class SetDeanAccountStateUseCase
{
    /// <summary>US-012 TEST_WRITING skeleton (OD-005): IMPLEMENTATION turns this into a primary constructor
    /// holding the dependencies. They are listed here so the tests construct the type exactly as it will be.</summary>
    public SetDeanAccountStateUseCase(
        IReadOnlyModeGuard readOnlyMode,
        IAppUserRepository users,
        IAuditEventRepository auditEvents,
        IUnitOfWork unitOfWork,
        ServiceWriteScope writeScope,
        TimeProvider timeProvider)
    {
    }

    public const string Operation = "DeanAccount.SetState";

    /// <param name="disable">True to disable the account, false to re-enable it.</param>
    public Task<DeanAccountActionOutcome> ExecuteAsync(
        long adminId,
        long deanId,
        bool disable,
        string? requestId,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-012 IMPLEMENTATION (spec FR-007, FR-008).");
}
