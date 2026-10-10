namespace ClassroomAgent.Application.Models;

/// <summary>
/// The units of one purge run, in the order US-037 spec FR-009 fixes; a failure names the one it happened in. US-031
/// spec FR-013 adds the expired Meet meetings, a batch at a time, after the leavers (db-design §6). US-032 adds the
/// orphaned "not a course" marks after the meetings.
/// </summary>
public enum RetentionPurgeStep
{
    ExpiredCourse,
    Leavers,
    ExpiredMeetings,

    /// <summary>US-032 spec FR-016 rule 4: "not a course" marks whose code has no meeting left.</summary>
    OrphanedMarks,
    OrphanedParticipants,
    Accounts,
    AuditRows,
    RunAuditEvent,
}
