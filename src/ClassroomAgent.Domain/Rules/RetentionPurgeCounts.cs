namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// The counts one retention purge run records in its audit event (US-037 spec FR-010, db-design §2.2; US-031 spec FR-013 adds the meetings and their connections): rows
/// committed as deleted, never a rolled-back unit (VR-003). Integers only, so no personal datum can enter them.
/// </summary>
public readonly record struct RetentionPurgeCounts(
    int Courses,
    int LeaverMemberships,
    int Participants,
    int Accounts,
    int AuditRows,
    int MeetSessions,
    int MeetParticipations,
    int MeetCodeLinks = 0)
{
    /// <summary>A run that removed nothing.</summary>
    public static RetentionPurgeCounts Zero => default;
}
