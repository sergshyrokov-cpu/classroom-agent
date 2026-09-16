namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>Who performed an audited action, stored as <c>owner</c> or <c>anonymous</c>.</summary>
public enum AuditActorType
{
    Owner,
    Anonymous,
}
