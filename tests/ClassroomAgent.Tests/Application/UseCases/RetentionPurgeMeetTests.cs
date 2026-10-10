using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.RetentionPurgeTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 AC-011, spec FR-013, BR-066, PC-11, db-design §6: the daily purge deletes every meeting whose own date
/// (<c>started_at</c>) is before the cutoff, with all its connections, in batches of 500, and the run's audit row carries
/// both counts. One purge run through the use case the host registers, over the host's real PostgreSQL database
/// (TC-2); meetings are seeded with raw SQL around the one cutoff <see cref="Cutoff"/>.
/// </summary>
public sealed class RetentionPurgeMeetTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AnOldMeeting_IsDeletedWithAllItsParticipations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var session = await SeedMeetingAsync(host, 1, Old, 3, ct);

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("meet_session", ct, "id = @id", ("id", session)));
        Assert.Equal(0L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id", ("id", session)));
    }

    [Fact]
    public async Task ARecentMeeting_IsKeptWithAllItsParticipations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var session = await SeedMeetingAsync(host, 1, Recent, 3, ct);

        await host.RunPurgeAsync(ct);

        Assert.Equal(1L, await host.CountAsync("meet_session", ct, "id = @id", ("id", session)));
        Assert.Equal(3L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id", ("id", session)));
    }

    /// <summary>FR-013, BR-066: a meeting dated exactly at the cutoff is kept; one second earlier is expired.</summary>
    [Fact]
    public async Task AtTheCutoff_TheMeetingIsKept_AndOneSecondEarlierItIsDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        var atCutoff = await SeedMeetingAsync(host, 1, Cutoff, 1, ct);
        var justBefore = await SeedMeetingAsync(host, 2, Cutoff - TimeSpan.FromSeconds(1), 1, ct);

        await host.RunPurgeAsync(ct);

        Assert.Equal(1L, await host.CountAsync("meet_session", ct, "id = @id", ("id", atCutoff)));
        Assert.Equal(1L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id", ("id", atCutoff)));
        Assert.Equal(0L, await host.CountAsync("meet_session", ct, "id = @id", ("id", justBefore)));
        Assert.Equal(0L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id", ("id", justBefore)));
    }

    /// <summary>FR-013: the audit row says how many meetings and how many connections the run deleted.</summary>
    [Fact]
    public async Task TheAuditRow_CarriesTheMeetingAndConnectionCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        await SeedMeetingAsync(host, 1, Old, 3, ct);
        await SeedMeetingAsync(host, 2, Old, 2, ct);
        await SeedMeetingAsync(host, 3, Recent, 4, ct);

        await host.RunPurgeAsync(ct);

        Assert.Equal((2, 5), await MeetCountsAsync(host, ct));
    }

    /// <summary>db-design §6: more than one batch of 500 — all of them go, and the counts add up.</summary>
    [Fact]
    public async Task MoreThanOneBatch_IsDeletedInFull_AndCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);
        await host.ExecuteAsync(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            SELECT 'bulk-' || g, 'abc-0001-xyz', 'teacher@school-one.example.test', @old, @old, @old, @old
            FROM generate_series(1, 501) AS g
            """,
            ct,
            ("old", Old));
        await host.ExecuteAsync(
            """
            INSERT INTO meet_participation (meet_session_id, endpoint_id, email, joined_at, duration_seconds, created_at, updated_at)
            SELECT id, 'endpoint-' || id, NULL, started_at, 60, @old, @old FROM meet_session
            """,
            ct,
            ("old", Old));
        Assert.Equal(501L, await host.CountAsync("meet_participation", ct));

        await host.RunPurgeAsync(ct);

        Assert.Equal(0L, await host.CountAsync("meet_session", ct));
        Assert.Equal(0L, await host.CountAsync("meet_participation", ct));
        Assert.Equal((501, 501), await MeetCountsAsync(host, ct));
    }

    /// <summary>FR-013, AC-011: a run that removed no meeting records zero and zero, not "absent".</summary>
    [Fact]
    public async Task APurgeWithNoMeetings_RecordsZeroAndZero()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct);

        await host.RunPurgeAsync(ct);

        Assert.Equal((0, 0), await MeetCountsAsync(host, ct));
    }

    /// <summary>BR-026, FR-013: the purge is a permitted service write, so in every read-only cause it still deletes.</summary>
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task InReadOnlyMode_AnOldMeetingIsStillDeleted(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await RetentionPurgeHost.StartWithoutPurgeServiceAsync(database, ct, cause);
        var session = await SeedMeetingAsync(host, 1, Old, 2, ct);

        var outcome = await host.RunPurgeAsync(ct);

        Assert.Empty(outcome.Failures);
        Assert.Equal(0L, await host.CountAsync("meet_session", ct, "id = @id", ("id", session)));
        Assert.Equal(0L, await host.CountAsync("meet_participation", ct, "meet_session_id = @id", ("id", session)));
        Assert.Equal((1, 2), await MeetCountsAsync(host, ct));
    }

    /// <summary>A meeting dated <paramref name="startedAt"/> with that many connections; returns the session id.</summary>
    private static async Task<long> SeedMeetingAsync(
        InstallationTestHost host,
        int conference,
        DateTimeOffset startedAt,
        int participations,
        CancellationToken cancellationToken)
    {
        var session = await host.ScalarAsync<long>(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            VALUES (@conferenceId, @meetingCode, @organizer, @startedAt, @endedAt, @startedAt, @startedAt)
            RETURNING id
            """,
            cancellationToken,
            ("conferenceId", MeetTestData.ConferenceId(conference)),
            ("meetingCode", MeetTestData.MeetingCode(conference)),
            ("organizer", MeetTestData.Teacher(1)),
            ("startedAt", startedAt),
            ("endedAt", startedAt + TimeSpan.FromHours(1)));

        for (var endpoint = 1; endpoint <= participations; endpoint++)
        {
            await host.ExecuteAsync(
                """
                INSERT INTO meet_participation (meet_session_id, endpoint_id, email, joined_at, duration_seconds, created_at, updated_at)
                VALUES (@sessionId, @endpointId, NULL, @startedAt, 600, @startedAt, @startedAt)
                """,
                cancellationToken,
                ("sessionId", session),
                ("endpointId", MeetTestData.EndpointId(endpoint)),
                ("startedAt", startedAt));
        }

        return session;
    }

    /// <summary>The two Meet counts of the one purge audit row, read directly (the shared reader has no such columns).</summary>
    private static async Task<(int? Sessions, int? Participations)> MeetCountsAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            """
            SELECT purged_meet_sessions, purged_meet_participations
            FROM audit_event WHERE action = 'retention_purge_run' ORDER BY id
            """,
            r => (Sessions: r.IsDBNull(0) ? (int?)null : r.GetInt32(0), Participations: r.IsDBNull(1) ? (int?)null : r.GetInt32(1)),
            cancellationToken);
        return Assert.Single(rows);
    }
}
