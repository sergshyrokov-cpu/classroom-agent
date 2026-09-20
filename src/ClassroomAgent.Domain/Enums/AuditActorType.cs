namespace ClassroomAgent.Domain.Enums;

/// <summary>Who performed an audited action (US-008 spec FR-012). <c>System</c> is written first by the retention purge (PC-11).</summary>
public enum AuditActorType
{
    AppUser,
    Anonymous,
    System,
}
