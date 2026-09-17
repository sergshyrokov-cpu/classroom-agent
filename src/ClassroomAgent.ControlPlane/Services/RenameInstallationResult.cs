namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Result of <see cref="InstallationRegistry.RenameAsync"/> (entity model §4).</summary>
public enum RenameInstallationResult
{
    Renamed,
    Unchanged,
    NotFound,
}
