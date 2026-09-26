using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The six-step sign-in sequence of SC-2, in order (US-012 spec FR-012, FR-013). It is deliberately not built
/// on <c>SignInManager</c>: that checks <c>CanSignInAsync</c> before the lockout and resets the counter on a
/// correct password, which would break steps 2 and 4 (SC-2, spec I-1, S-09).
/// </summary>
/// <remarks>
/// Its writes — the failed-attempt counter, the lockout and the last successful sign-in — are sign-in
/// bookkeeping, one of the closed list of service writes permitted in read-only mode (BR-026, spec FR-015), so
/// this use case consults no read-only guard.
/// US-012 TEST_WRITING skeleton (OD-005) — IMPLEMENTATION writes the body.
/// </remarks>
public sealed class SignInDeanUseCase
{
    /// <summary>US-012 TEST_WRITING skeleton (OD-005): IMPLEMENTATION turns this into a primary constructor
    /// holding the dependencies. They are listed here so the tests construct the type exactly as it will be.</summary>
    public SignInDeanUseCase(
        IAppUserRepository users,
        IAuditEventRepository auditEvents,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ServiceWriteScope writeScope,
        TimeProvider timeProvider)
    {
    }

    /// <summary>Consecutive failures that lock sign-in (SC-2).</summary>
    public const int MaximumFailedAttempts = 5;

    /// <summary>How long the lockout lasts. It expires by itself; there is no permanent lockout (SC-2 v62).</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public Task<DeanSignInOutcome> ExecuteAsync(
        string email,
        string password,
        string? requestId,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException("US-012 IMPLEMENTATION (spec FR-012).");
}
