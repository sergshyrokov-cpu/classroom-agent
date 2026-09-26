namespace ClassroomAgent.Application.Models;

/// <summary>
/// The result of a password change by the Dean — the forced one or a later voluntary one (US-012 spec FR-006,
/// FR-014). No field of it ever carries a password or a hash (spec S-10).
/// </summary>
/// <param name="Refused">True when nothing was written.</param>
/// <param name="WrongCurrentPassword">The voluntary change only: the current password did not verify.</param>
/// <param name="Violation">Which policy rule the new password broke, when it broke one.</param>
public sealed record PasswordChangeOutcome(
    bool Refused,
    bool WrongCurrentPassword,
    PasswordPolicyViolation? Violation);
