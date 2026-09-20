namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// The audited actions of the installation (US-008 spec FR-012; <c>trebovaniya.md</c> §5). The list grows in
/// the Story that performs the action, never ahead of it (spec I-11).
/// </summary>
public enum AuditAction
{
    AdminSignIn,
}
