namespace ClassroomAgent.Application.Models;

/// <summary>
/// The units of one purge run, in the order US-037 spec FR-009 fixes; a failure names the one it happened in. US-031
/// spec FR-013 adds the expired Meet meetings, a batch at a time, after the leavers (db-design §6).
/// </summary>
public enum RetentionPurgeStep
{
    ExpiredCourse,
    Leavers,
    ExpiredMeetings,
    OrphanedParticipants,
    Accounts,
    AuditRows,
    RunAuditEvent,
}
