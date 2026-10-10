using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData.Names;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-031 AC-013, db-design §2 … §5 and §10, against the migrated database (TC-2): the two new tables hold exactly the
/// columns the design fixes and no telemetry, device, location or display-name column; every unique index, check
/// constraint and foreign key rejects a violating write by its own name; the three indexes are on the columns named;
/// and the two columns each of <c>sync_state</c> and <c>audit_event</c> gained carry their constraints.
/// </summary>
public sealed class MeetSchemaTests(PostgreSqlFixture database)
{
    private const string InsertSession =
        """
        INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
        VALUES (@conferenceId, @meetingCode, @organizer, @startedAt, @endedAt, @startedAt, @startedAt)
        RETURNING id
        """;

    private const string InsertParticipation =
        """
        INSERT INTO meet_participation (meet_session_id, endpoint_id, email, joined_at, duration_seconds, created_at, updated_at)
        VALUES (@sessionId, @endpointId, @email, @joinedAt, @duration, @joinedAt, @joinedAt)
        """;

    private const string InsertSyncState =
        """
        INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count, last_error,
                                last_successful_run_at, meet_loaded_up_to, failed_step, created_at, updated_at)
        VALUES (true, @status, @runId, @at, @at, 0, @lastError, @lastSuccessfulRunAt, @meetLoadedUpTo, @failedStep, @at, @at)
        """;

    private const string InsertAuditRow =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, purged_courses, purged_leaver_memberships,
                                 purged_participants, purged_accounts, purged_audit_rows, purged_meet_sessions,
                                 purged_meet_participations, created_at, updated_at)
        VALUES (now(), @actorType, @actorId, @actorRole, @action, @targetType, @targetId, 'succeeded', NULL, @requestId,
                @courses, @courses, @courses, @courses, @courses, @sessions, @participations, now(), now())
        """;

    private static readonly DateTimeOffset At = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    // ---------------------------------------------------------------- columns (db-design §2.1, §3.1, AC-013)

    /// <summary>db-design §2.1, AC-013: exactly eight columns, with the types, lengths and nullability fixed.</summary>
    [Fact]
    public async Task MeetSession_HasExactlyTheDesignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await ColumnsAsync(host, SessionTable, ct);

        Assert.Equal(
            new[]
            {
                "id bigint - NO",
                "conference_id character varying 128 NO",
                "meeting_code character varying 64 NO",
                "organizer_email character varying 254 NO",
                "started_at timestamp with time zone - NO",
                "ended_at timestamp with time zone - NO",
                "created_at timestamp with time zone - NO",
                "updated_at timestamp with time zone - NO",
            }.Order(StringComparer.Ordinal),
            columns);
    }

    /// <summary>db-design §3.1, AC-013: exactly eight columns; the email is the only nullable one.</summary>
    [Fact]
    public async Task MeetParticipation_HasExactlyTheDesignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await ColumnsAsync(host, ParticipationTable, ct);

        Assert.Equal(
            new[]
            {
                "id bigint - NO",
                "meet_session_id bigint - NO",
                "endpoint_id character varying 128 NO",
                "email character varying 254 YES",
                "joined_at timestamp with time zone - NO",
                "duration_seconds integer - NO",
                "created_at timestamp with time zone - NO",
                "updated_at timestamp with time zone - NO",
            }.Order(StringComparer.Ordinal),
            columns);
    }

    // ---------------------------------------------------------------- meet_session constraints (db-design §2.2)

    [Fact]
    public async Task ADuplicateConferenceId_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertSessionAsync(host, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertSessionAsync(host, ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Equal(SessionConferenceUnique, failure.ConstraintName);
    }

    [Fact]
    public async Task ASessionEndingBeforeItStarted_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSessionAsync(host, ct, endedAt: At - TimeSpan.FromSeconds(1)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(SessionEndedAfterStarted, failure.ConstraintName);
    }

    [Fact]
    public async Task ASessionEndingWhenItStarted_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertSessionAsync(host, ct, endedAt: At);

        Assert.Equal(1L, await host.CountAsync(SessionTable, ct));
    }

    [Theory]
    [InlineData("", "abc-0001-xyz", "teacher@school-one.example.test")]
    [InlineData("conf-0001-synthetic", "", "teacher@school-one.example.test")]
    [InlineData("conf-0001-synthetic", "abc-0001-xyz", "ab")]
    public async Task ABlankSessionValue_IsRejected(string conferenceId, string meetingCode, string organizer)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSessionAsync(host, ct, conferenceId: conferenceId, meetingCode: meetingCode, organizer: organizer));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(SessionValues, failure.ConstraintName);
    }

    // ---------------------------------------------------------------- meet_participation constraints (db-design §3.2)

    [Fact]
    public async Task ADuplicateEndpointInOneSession_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var session = await InsertSessionAsync(host, ct);
        await InsertParticipationAsync(host, session, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertParticipationAsync(host, session, ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Equal(ParticipationEndpointUnique, failure.ConstraintName);
    }

    [Fact]
    public async Task TheSameEndpointInAnotherSession_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var first = await InsertSessionAsync(host, ct);
        var second = await InsertSessionAsync(host, ct, conferenceId: MeetTestData.ConferenceId(2));
        await InsertParticipationAsync(host, first, ct);

        await InsertParticipationAsync(host, second, ct);

        Assert.Equal(2L, await host.CountAsync(ParticipationTable, ct));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(86_401)]
    public async Task ADurationOutOfRange_IsRejected(int duration)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var session = await InsertSessionAsync(host, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertParticipationAsync(host, session, ct, duration: duration));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(ParticipationDuration, failure.ConstraintName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(86_400)]
    public async Task ADurationAtTheBounds_IsAccepted(int duration)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var session = await InsertSessionAsync(host, ct);

        await InsertParticipationAsync(host, session, ct, duration: duration);

        Assert.Equal(1L, await host.CountAsync(ParticipationTable, ct));
    }

    [Theory]
    [InlineData("", "teacher@school-one.example.test")]
    [InlineData("endpoint-0001", "ab")]
    public async Task ABlankParticipationValue_IsRejected(string endpointId, string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var session = await InsertSessionAsync(host, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertParticipationAsync(host, session, ct, endpointId: endpointId, email: email));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(ParticipationValues, failure.ConstraintName);
    }

    /// <summary>db-design §3.1: a null email is the "other participant" and is a valid row.</summary>
    [Fact]
    public async Task ANullEmail_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var session = await InsertSessionAsync(host, ct);

        await InsertParticipationAsync(host, session, ct, email: null);

        Assert.Equal(1L, await host.CountAsync(ParticipationTable, ct, "email IS NULL"));
    }

    /// <summary>db-design §3.2, PC-8: the foreign key is RESTRICT, so the purge must delete children first.</summary>
    [Fact]
    public async Task ASessionWithParticipations_CannotBeDeleted_UntilTheyAreGone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var session = await InsertSessionAsync(host, ct);
        await InsertParticipationAsync(host, session, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => host.ExecuteAsync("DELETE FROM meet_session WHERE id = @id", ct, ("id", session)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        Assert.Equal(ParticipationSessionForeignKey, failure.ConstraintName);

        await host.ExecuteAsync("DELETE FROM meet_participation WHERE meet_session_id = @id", ct, ("id", session));
        await host.ExecuteAsync("DELETE FROM meet_session WHERE id = @id", ct, ("id", session));
        Assert.Equal(0L, await host.CountAsync(SessionTable, ct));
    }

    // ---------------------------------------------------------------- indexes (db-design §2.3, §3.3)

    [Theory]
    [InlineData(SessionStartedAtIndex, "(started_at)")]
    [InlineData(SessionCodeStartedAtIndex, "(meeting_code, started_at)")]
    [InlineData(ParticipationEmailJoinedAtIndex, "(email, joined_at)")]
    public async Task TheIndex_IsOnExactlyTheDesignedColumns(string index, string columns)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = await host.ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND indexname = @name",
            ct,
            ("name", index));

        Assert.NotNull(definition);
        Assert.EndsWith(columns, definition, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- sync_state (db-design §4)

    [Fact]
    public async Task SyncState_GainsTheWatermarkAndTheFailedStep_BothNullable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await ColumnsAsync(host, "sync_state", ct);

        Assert.Contains("meet_loaded_up_to timestamp with time zone - YES", columns);
        Assert.Contains("failed_step character varying 16 YES", columns);
    }

    [Theory]
    [InlineData("completed", null, "meet")]
    [InlineData("failed", "ScopeNotAuthorized", "other")]
    public async Task AFailedStepOutsideItsRule_IsRejected(string status, string? lastError, string step)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSyncStateAsync(host, ct, status, lastError, step, meetLoadedUpTo: null, lastSuccessfulRunAt: At));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(SyncFailedStep, failure.ConstraintName);
    }

    /// <summary>db-design §4.2: a failed row names its step, or names none when an earlier version wrote it.</summary>
    [Theory]
    [InlineData("meet")]
    [InlineData("classroom")]
    [InlineData(null)]
    public async Task AFailedRow_WithAStepOrNone_IsAccepted(string? step)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertSyncStateAsync(host, ct, "failed", "ScopeNotAuthorized", step, meetLoadedUpTo: null, lastSuccessfulRunAt: null);

        Assert.Equal(1L, await host.CountAsync("sync_state", ct));
    }

    /// <summary>db-design §4.2, FR-007: a watermark is only ever written by a completed run.</summary>
    [Fact]
    public async Task AWatermark_WithoutALastSuccessfulRun_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => InsertSyncStateAsync(host, ct, "failed", "ScopeNotAuthorized", "classroom", meetLoadedUpTo: At, lastSuccessfulRunAt: null));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(SyncMeetLoadedUpTo, failure.ConstraintName);
    }

    [Fact]
    public async Task AWatermark_WithALastSuccessfulRun_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertSyncStateAsync(host, ct, "failed", "ScopeNotAuthorized", "meet", meetLoadedUpTo: At, lastSuccessfulRunAt: At);

        Assert.Equal(1L, await host.CountAsync("sync_state", ct, "meet_loaded_up_to IS NOT NULL"));
    }

    // ---------------------------------------------------------------- audit_event (db-design §5)

    [Fact]
    public async Task AuditEvent_GainsTwoNullableCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await ColumnsAsync(host, "audit_event", ct);

        Assert.Contains("purged_meet_sessions integer - YES", columns);
        Assert.Contains("purged_meet_participations integer - YES", columns);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(null, 1)]
    public async Task OnlyOneOfTheTwoMeetCounts_IsRejected(int? sessions, int? participations)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertPurgeRowAsync(host, ct, sessions, participations));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditMeetCounts, failure.ConstraintName);
    }

    [Fact]
    public async Task MeetCounts_OnAnotherAction_AreRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => host.ExecuteAsync(
            InsertAuditRow,
            ct,
            ("actorType", "app_user"),
            ("actorId", 1L),
            ("actorRole", "admin"),
            ("action", "workspace_connection_saved"),
            ("targetType", "workspace_connection"),
            ("targetId", 1L),
            ("requestId", "request-id"),
            ("courses", null),
            ("sessions", 1),
            ("participations", 1)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditMeetCountsAbsent, failure.ConstraintName);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task ANegativeMeetCount_IsRejected(int sessions, int participations)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertPurgeRowAsync(host, ct, sessions, participations));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditMeetCountsNonNegative, failure.ConstraintName);
    }

    /// <summary>db-design §5.2: a purge row written before this Story carries both counts as null and stays valid.</summary>
    [Fact]
    public async Task APurgeRow_WithBothMeetCountsNull_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertPurgeRowAsync(host, ct, null, null);

        Assert.Single(await host.PurgeAuditRowsAsync(ct));
    }

    [Fact]
    public async Task APurgeRow_WithBothMeetCounts_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertPurgeRowAsync(host, ct, 3, 7);

        Assert.Equal(1L, await host.CountAsync("audit_event", ct, "purged_meet_sessions = 3 AND purged_meet_participations = 7"));
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>"name type length nullable" per column, sorted ordinally; "-" where there is no length.</summary>
    private static async Task<IReadOnlyList<string>> ColumnsAsync(
        InstallationTestHost host,
        string table,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            """
            SELECT column_name, data_type, character_maximum_length, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            """,
            r => $"{r.GetString(0)} {r.GetString(1)} {(r.IsDBNull(2) ? "-" : r.GetInt32(2).ToString(System.Globalization.CultureInfo.InvariantCulture))} {r.GetString(3)}",
            cancellationToken,
            ("table", table));
        return rows.Order(StringComparer.Ordinal).ToList();
    }

    private static Task<long> InsertSessionAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string conferenceId = "conf-0001-synthetic",
        string meetingCode = "abc-0001-xyz",
        string organizer = "teacher@school-one.example.test",
        DateTimeOffset? endedAt = null) =>
        host.ScalarAsync<long>(
            InsertSession,
            cancellationToken,
            ("conferenceId", conferenceId),
            ("meetingCode", meetingCode),
            ("organizer", organizer),
            ("startedAt", At),
            ("endedAt", endedAt ?? At + TimeSpan.FromHours(1)));

    private static Task<int> InsertParticipationAsync(
        InstallationTestHost host,
        long sessionId,
        CancellationToken cancellationToken,
        string endpointId = "endpoint-0001",
        string? email = "student@school-one.example.test",
        int duration = 600) =>
        host.ExecuteAsync(
            InsertParticipation,
            cancellationToken,
            ("sessionId", sessionId),
            ("endpointId", endpointId),
            ("email", email),
            ("joinedAt", At),
            ("duration", duration));

    private static Task<int> InsertSyncStateAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string status,
        string? lastError,
        string? failedStep,
        DateTimeOffset? meetLoadedUpTo,
        DateTimeOffset? lastSuccessfulRunAt) =>
        host.ExecuteAsync(
            InsertSyncState,
            cancellationToken,
            ("status", status),
            ("runId", Guid.NewGuid()),
            ("at", At),
            ("lastError", lastError),
            ("lastSuccessfulRunAt", lastSuccessfulRunAt),
            ("meetLoadedUpTo", meetLoadedUpTo),
            ("failedStep", failedStep));

    /// <summary>A purge row of the shape RetentionPurgeSchemaTests inserts, with the two Meet counts given.</summary>
    private static Task<int> InsertPurgeRowAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        int? sessions,
        int? participations) =>
        host.ExecuteAsync(
            InsertAuditRow,
            cancellationToken,
            ("actorType", "system"),
            ("actorId", null),
            ("actorRole", null),
            ("action", RetentionPurgeTestData.Action),
            ("targetType", null),
            ("targetId", null),
            ("requestId", null),
            ("courses", 1),
            ("sessions", sessions),
            ("participations", participations));
}
