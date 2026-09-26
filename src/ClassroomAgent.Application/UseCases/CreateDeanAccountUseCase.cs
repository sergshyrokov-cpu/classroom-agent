using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// An Admin creates a Dean account (US-012 spec FR-003). The evaluation order is fixed: the read-only guard
/// first, before any repository call, then the address, the school's domain, the password policy and finally
/// uniqueness. On success the account and its audit row commit in one transaction and nothing else is staged
/// (carried US-009 F-2).
/// </summary>
/// <remarks>US-012 TEST_WRITING skeleton (OD-005) — IMPLEMENTATION writes the body.</remarks>
public sealed class CreateDeanAccountUseCase
{
    /// <summary>US-012 TEST_WRITING skeleton (OD-005): IMPLEMENTATION turns this into a primary constructor
    /// holding the dependencies. They are listed here so the tests construct the type exactly as it will be.</summary>
    public CreateDeanAccountUseCase(
        IReadOnlyModeGuard readOnlyMode,
        IAppUserRepository users,
        ILegitimacyStateRepository legitimacyStates,
        IAuditEventRepository auditEvents,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ServiceWriteScope writeScope,
        TimeProvider timeProvider)
    {
    }

    /// <summary>The operation name the read-only guard records; a constant, never user data (SC-10).</summary>
    public const string Operation = "DeanAccount.Create";

    public Task<DeanAccountActionOutcome> ExecuteAsync(
        long adminId,
        string email,
        string temporaryPassword,
        string? requestId,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-012 IMPLEMENTATION (spec FR-003).");
}
