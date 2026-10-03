namespace ClassroomAgent.Application.Models;

/// <summary>The units of one purge run, in the order US-037 spec FR-009 fixes; a failure names the one it happened in.</summary>
public enum RetentionPurgeStep
{
    ExpiredCourse,
    Leavers,
    OrphanedParticipants,
    Accounts,
    AuditRows,
    RunAuditEvent,
}
