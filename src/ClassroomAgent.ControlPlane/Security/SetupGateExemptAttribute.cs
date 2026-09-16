namespace ClassroomAgent.ControlPlane.Security;

/// <summary>Marks an endpoint the setup gate lets through before the Owner account exists (FR-001).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SetupGateExemptAttribute : Attribute
{
}
