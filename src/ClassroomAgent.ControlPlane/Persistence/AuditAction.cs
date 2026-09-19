namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>Audited Control Plane actions, stored as snake_case codes (db-design §4.1).</summary>
public enum AuditAction
{
    OwnerSignIn,
    OwnerFirstRunSetup,
    InstallationCreated,
    InstallationRenamed,
    InstallationClientIdChanged,
    InstallationPushAddressChanged,
    AllowedAdminAdded,
    AllowedAdminRevoked,
    InstallationSuspended,
    InstallationResumed,
}
