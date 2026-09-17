namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>Status of an installation, stored as <c>active</c> or <c>suspended</c> (entity model §2.2).</summary>
public enum InstallationStatus
{
    Active,
    Suspended,
}
