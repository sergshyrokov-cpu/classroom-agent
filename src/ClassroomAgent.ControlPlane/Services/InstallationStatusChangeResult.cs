namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Result of <see cref="InstallationStatusService.ChangeStatusAsync"/> (US-004 entity model §4).</summary>
public enum InstallationStatusChangeResult
{
    NotFound,
    Changed,
    Unchanged,
}
