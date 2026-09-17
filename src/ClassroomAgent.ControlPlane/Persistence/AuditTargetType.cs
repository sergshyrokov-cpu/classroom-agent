namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>Entity type of an audit target, stored as <c>owner</c>, <c>installation</c> or <c>allowed_admin</c>.</summary>
public enum AuditTargetType
{
    Owner,
    Installation,
    AllowedAdmin,
}
