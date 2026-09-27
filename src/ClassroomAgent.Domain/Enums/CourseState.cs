namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// The state of a Classroom course (US-014 entity model §2; <c>trebovaniya.md</c> §3). Exactly five members —
/// no <c>Unknown</c>, no <c>None</c>, no <c>Unspecified</c>: OD-010 skips a course whose state Classroom reports
/// outside these five before it becomes an entity, so there is no state left for the enum to represent, and a
/// sixth member would be a business rule no artifact defines (<c>AGENTS.md</c> Hard Stops). The mapping to the
/// lower-case database codes is a value converter in the configuration, exactly as <c>SyncRunStatus</c> does it.
/// </summary>
public enum CourseState
{
    Active,
    Archived,
    Provisioned,
    Declined,
    Suspended,
}
