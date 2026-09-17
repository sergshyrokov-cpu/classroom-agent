namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>
/// Compatibility of an installation's versions with the Control Plane, stored as <c>supported</c>,
/// <c>upgrade_recommended</c> or <c>upgrade_required</c> (US-005 entity model §2.2, DC-12).
/// </summary>
public enum CompatibilityState
{
    Supported,
    UpgradeRecommended,
    UpgradeRequired,
}
