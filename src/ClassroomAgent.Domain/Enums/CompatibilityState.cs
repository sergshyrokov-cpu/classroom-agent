namespace ClassroomAgent.Domain.Enums;

/// <summary>Compatibility state answered by the Control Plane (DC-12; US-005 entity model §3.2).</summary>
public enum CompatibilityState
{
    Supported,
    UpgradeRecommended,
    UpgradeRequired,
}
