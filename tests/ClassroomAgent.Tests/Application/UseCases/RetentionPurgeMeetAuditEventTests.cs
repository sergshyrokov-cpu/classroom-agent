using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 AC-011 (spec FR-013, OD-008; entity model §4, db-design §5; SC-11): the purge's single audit row carries how
/// many meetings and Meet connections it deleted, guarded as non-negative like the five US-037 counts; no other action
/// carries them.
/// </summary>
public sealed class RetentionPurgeMeetAuditEventTests
{
    private static readonly DateTimeOffset At = InstallationTestHost.DefaultStart;

    [Fact]
    public void ThePurgeRow_CarriesTheMeetCounts()
    {
        var row = AuditEvent.RetentionPurgeRun(new RetentionPurgeCounts(1, 2, 3, 4, 5, 6, 7), At);

        Assert.Equal(6, row.PurgedMeetSessions);
        Assert.Equal(7, row.PurgedMeetParticipations);
        Assert.Equal(5, row.PurgedAuditRows);
    }

    /// <summary>A purge that deleted no meeting still records both counts, as zero — never null (db-design §5.2).</summary>
    [Fact]
    public void APurgeWithNoMeetings_RecordsZeroNotNull()
    {
        var row = AuditEvent.RetentionPurgeRun(RetentionPurgeCounts.Zero, At);

        Assert.Equal(0, row.PurgedMeetSessions);
        Assert.Equal(0, row.PurgedMeetParticipations);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void ANegativeMeetCount_IsRefused(int sessions, int participations) =>
        Assert.ThrowsAny<ArgumentException>(
            () => AuditEvent.RetentionPurgeRun(new RetentionPurgeCounts(0, 0, 0, 0, 0, sessions, participations), At));

    /// <summary>db-design §5.2: the Meet counts belong to the purge row only.</summary>
    [Fact]
    public void AnotherAction_CarriesNoMeetCounts()
    {
        var row = AuditEvent.AdminSignInSucceeded(1, At, "request-id");

        Assert.Null(row.PurgedMeetSessions);
        Assert.Null(row.PurgedMeetParticipations);
    }
}
