namespace ClassroomAgent.Application.Models;

/// <summary>Why the installation is in read-only mode (US-005 spec FR-008).</summary>
public enum LegitimacyModeReason
{
    NotYetConfirmed,
    SuspendedByOwner,
    GracePeriodExpired,
}
