namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// The rows one erasure deleted or cleared, as its audit event and log line record them (US-038 entity model §4;
/// spec FR-013): integers only, so no personal datum can enter them. Each is non-negative.
/// </summary>
public sealed record ErasureCounts
{
    public ErasureCounts(
        int memberships,
        int submissions,
        int meetParticipations,
        int clearedMeetOrganizers,
        int clearedCourseOwners)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(memberships);
        ArgumentOutOfRangeException.ThrowIfNegative(submissions);
        ArgumentOutOfRangeException.ThrowIfNegative(meetParticipations);
        ArgumentOutOfRangeException.ThrowIfNegative(clearedMeetOrganizers);
        ArgumentOutOfRangeException.ThrowIfNegative(clearedCourseOwners);

        Memberships = memberships;
        Submissions = submissions;
        MeetParticipations = meetParticipations;
        ClearedMeetOrganizers = clearedMeetOrganizers;
        ClearedCourseOwners = clearedCourseOwners;
    }

    public int Memberships { get; }

    public int Submissions { get; }

    public int MeetParticipations { get; }

    public int ClearedMeetOrganizers { get; }

    public int ClearedCourseOwners { get; }
}
