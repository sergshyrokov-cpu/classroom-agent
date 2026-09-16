namespace ClassroomAgent.ControlPlane.Persistence;

/// <summary>Why an audited action was refused, stored as snake_case codes (db-design §4.1).</summary>
public enum AuditRefusalCategory
{
    UnknownLogin,
    WrongPassword,
    LockedOut,
    WrongSetupCode,
}
