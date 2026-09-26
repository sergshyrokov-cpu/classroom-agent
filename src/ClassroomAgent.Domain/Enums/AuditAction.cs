namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// The audited actions of the installation (US-008 spec FR-012; <c>trebovaniya.md</c> §5). The list grows in
/// the Story that performs the action, never ahead of it (spec I-11).
/// </summary>
public enum AuditAction
{
    AdminSignIn,

    /// <summary>
    /// US-009 spec FR-009: saving or changing the <c>WorkspaceConnection</c>. One action for both, as
    /// <c>trebovaniya.md</c> §5 names them in one breath; which it was follows from the row it targets (spec I-2).
    /// </summary>
    WorkspaceConnectionSaved,

    /// <summary>
    /// US-011 spec FR-008: running "Проверить доступ" (<c>trebovaniya.md</c> §5). The row records that the check was
    /// carried out or refused — never what it found (spec I-4, OD-003).
    /// </summary>
    AccessCheckRun,
}
