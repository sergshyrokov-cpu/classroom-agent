namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Result of <see cref="InstallationRegistry.ChangeClientIdAsync"/> (entity model §4).</summary>
public enum ChangeClientIdResult
{
    Changed,
    Unchanged,
    NotFound,
    ClientIdTaken,
}
