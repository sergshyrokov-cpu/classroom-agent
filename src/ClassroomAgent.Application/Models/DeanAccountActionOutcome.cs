namespace ClassroomAgent.Application.Models;

/// <summary>
/// What a management action did (US-012 spec FR-003, FR-007, FR-008, FR-009). A refusal is an outcome, not an
/// exception — exceptions signal failures, not expected results (AD-9).
/// </summary>
/// <param name="Refusal">Null when the action succeeded.</param>
/// <param name="Violation">Set only with <see cref="DeanAccountRefusal.PasswordPolicy"/>.</param>
/// <param name="AccountId">The account created or acted upon; null when nothing was touched.</param>
public sealed record DeanAccountActionOutcome(
    DeanAccountRefusal? Refusal,
    PasswordPolicyViolation? Violation,
    long? AccountId);
