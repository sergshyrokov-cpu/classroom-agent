using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData.Names;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-038 db-design §2 … §6, §9, §11 and spec AC-006, S-05 against the migrated database (TC-2): the two new tables hold
/// exactly the designed columns — no name, no email; every unique index and check rejects a violating write by its own
/// name; an exclusion and the salt can be created and deleted but never changed; the organizer email of a meeting may be
/// empty; the three indexes exist on the designed expressions; the audit checks accept the designed rows and reject the
/// others.
/// </summary>
public sealed class ParticipantErasureSchemaTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset At = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    private static Task<long> InsertExclusionAsync(
        InstallationTestHost host,
        CancellationToken ct,
        string googleUserId = "100000000000000041",
        byte[]? fingerprint = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null) =>
        host.ScalarAsync<long>(
            """
            INSERT INTO erased_participant (google_user_id, email_fingerprint, erased_at, created_at, updated_at)
            VALUES (@id, @fp, @at, @created, @updated)
            RETURNING id
            """,
            ct,
            ("id", googleUserId),
            ("fp", fingerprint),
            ("at", At),
            ("created", createdAt ?? At),
            ("updated", updatedAt ?? createdAt ?? At));

    private static Task<int> InsertSaltAsync(InstallationTestHost host, CancellationToken ct, byte[]? salt = null, bool singleton = true) =>
        host.ExecuteAsync(
            "INSERT INTO erasure_salt (singleton, salt, created_at, updated_at) VALUES (@singleton, @salt, @at, @at)",
            ct,
            ("singleton", singleton),
            ("salt", salt ?? ErasureTestData.KnownSalt()),
            ("at", At));

    private static async Task<PostgresException> RejectedAsync(Func<Task> write) =>
        await Assert.ThrowsAsync<PostgresException>(write);

    private static Task<string?> IndexDefinitionAsync(InstallationTestHost host, string index, CancellationToken ct) =>
        host.ScalarAsync<string>("SELECT indexdef FROM pg_indexes WHERE indexname = @name", ct, ("name", index));

    // ---------------------------------------------------------------- erased_participant (db-design §2)

    /// <summary>db-design §2.1, AC-006: exactly six columns — no name and no email.</summary>
    [Fact]
    public async Task TheExclusionTable_HasExactlyTheDesignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await InstallationSchemaQueries.ColumnsAsync(host, ExclusionTable, ct);

        Assert.Equal(
            new[]
            {
                "created_at timestamp with time zone - NO -",
                "email_fingerprint bytea - YES -",
                "erased_at timestamp with time zone - NO -",
                "google_user_id character varying 64 NO -",
                "id bigint - NO -",
                "updated_at timestamp with time zone - NO -",
            },
            columns);
    }

    /// <summary>db-design §2.2: one exclusion per Google user id.</summary>
    [Fact]
    public async Task ASecondExclusionOfOneGoogleUserId_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertExclusionAsync(host, ct);

        var failure = await RejectedAsync(() => InsertExclusionAsync(host, ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Equal(ExclusionGoogleUserIdUnique, failure.ConstraintName);
    }

    /// <summary>db-design §2.2, spec I-10: two exclusions may share a fingerprint.</summary>
    [Fact]
    public async Task TwoExclusionsWithOneFingerprint_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var fingerprint = ErasureTestData.Fingerprint("o.koval@school-one.example.test", ErasureTestData.KnownSalt());

        await InsertExclusionAsync(host, ct, "100000000000000041", fingerprint);
        await InsertExclusionAsync(host, ct, "100000000000000042", fingerprint);

        Assert.Equal(2L, await host.CountAsync(ExclusionTable, ct));
    }

    /// <summary>db-design §2.2: an empty Google user id and a fingerprint of another length are rejected.</summary>
    [Theory]
    [InlineData("", 32)]
    [InlineData("100000000000000041", 31)]
    [InlineData("100000000000000041", 33)]
    public async Task AnInvalidExclusion_IsRejected(string googleUserId, int fingerprintLength)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => InsertExclusionAsync(host, ct, googleUserId, new byte[fingerprintLength]));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(ExclusionValues, failure.ConstraintName);
    }

    /// <summary>db-design §2.2: an exclusion is never changed — an update moving updated_at is rejected; a delete is allowed.</summary>
    [Fact]
    public async Task AnExclusion_CannotBeChanged_ButCanBeDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var id = await InsertExclusionAsync(host, ct);

        var failure = await RejectedAsync(() => host.ExecuteAsync(
            "UPDATE erased_participant SET updated_at = @later WHERE id = @id", ct, ("later", At.AddMinutes(1)), ("id", id)));
        await host.ExecuteAsync("DELETE FROM erased_participant WHERE id = @id", ct, ("id", id));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(ExclusionImmutable, failure.ConstraintName);
        Assert.Equal(0L, await host.CountAsync(ExclusionTable, ct));
    }

    /// <summary>db-design §2.3: the unique Google-id index and the non-unique fingerprint index.</summary>
    [Fact]
    public async Task TheExclusionIndexes_AreOnTheDesignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var unique = await IndexDefinitionAsync(host, ExclusionGoogleUserIdUnique, ct);
        var fingerprint = await IndexDefinitionAsync(host, ExclusionFingerprintIndex, ct);

        Assert.NotNull(unique);
        Assert.Contains("UNIQUE", unique, StringComparison.Ordinal);
        Assert.Contains("(google_user_id)", unique, StringComparison.Ordinal);
        Assert.NotNull(fingerprint);
        Assert.DoesNotContain("UNIQUE", fingerprint, StringComparison.Ordinal);
        Assert.Contains("(email_fingerprint)", fingerprint, StringComparison.Ordinal);
    }

    /// <summary>db-design §2.1, PC-8: no foreign key — nothing it could point to survives the erasure.</summary>
    [Fact]
    public async Task TheExclusionTable_HasNoForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Empty(await InstallationSchemaQueries.ForeignKeysAsync(host, ExclusionTable, ct));
    }

    // ---------------------------------------------------------------- erasure_salt (db-design §3)

    /// <summary>db-design §3.1: the salt table's columns.</summary>
    [Fact]
    public async Task TheSaltTable_HasExactlyTheDesignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await InstallationSchemaQueries.ColumnsAsync(host, SaltTable, ct);

        Assert.Equal(5, columns.Count);
        Assert.Contains("id bigint - NO -", columns);
        Assert.Contains("salt bytea - NO -", columns);
        Assert.Contains(columns, c => c.StartsWith("singleton boolean - NO", StringComparison.Ordinal));
        Assert.Contains("created_at timestamp with time zone - NO -", columns);
        Assert.Contains("updated_at timestamp with time zone - NO -", columns);
    }

    /// <summary>db-design §3.3: the migration seeds no salt — the first erasure creates it.</summary>
    [Fact]
    public async Task TheMigration_SeedsNoSalt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(0L, await host.CountAsync(SaltTable, ct));
        Assert.Equal(0L, await host.CountAsync(ExclusionTable, ct));
    }

    /// <summary>db-design §3.2: at most one salt.</summary>
    [Fact]
    public async Task ASecondSalt_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertSaltAsync(host, ct);

        var failure = await RejectedAsync(() => InsertSaltAsync(host, ct, new byte[32]));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Equal(SaltSingletonUnique, failure.ConstraintName);
    }

    /// <summary>db-design §3.2: the singleton column must be true.</summary>
    [Fact]
    public async Task ANonSingletonSalt_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => InsertSaltAsync(host, ct, singleton: false));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(SaltSingleton, failure.ConstraintName);
    }

    /// <summary>db-design §3.2: the salt is 32 bytes.</summary>
    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public async Task ASaltOfAnotherLength_IsRejected(int length)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => InsertSaltAsync(host, ct, new byte[length]));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(SaltValues, failure.ConstraintName);
    }

    /// <summary>db-design §3.2, spec FR-010: the salt is never changed.</summary>
    [Fact]
    public async Task TheSalt_CannotBeChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertSaltAsync(host, ct);

        var failure = await RejectedAsync(() => host.ExecuteAsync(
            "UPDATE erasure_salt SET salt = @salt, updated_at = @later", ct, ("salt", new byte[32]), ("later", At.AddMinutes(1))));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(SaltImmutable, failure.ConstraintName);
    }

    // ---------------------------------------------------------------- meet_session, meet_participation, course (§4 … §6)

    /// <summary>db-design §4: a meeting may have no organizer email; a too-short one is still rejected.</summary>
    [Fact]
    public async Task AMeetingWithoutOrganizerEmail_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await host.ExecuteAsync(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            VALUES ('conf-0001-synthetic', 'abc-0001-xyz', NULL, @at, @at, @at, @at)
            """,
            ct,
            ("at", At));
        var failure = await RejectedAsync(() => host.ExecuteAsync(
            """
            INSERT INTO meet_session (conference_id, meeting_code, organizer_email, started_at, ended_at, created_at, updated_at)
            VALUES ('conf-0002-synthetic', 'abc-0002-xyz', 'ab', @at, @at, @at, @at)
            """,
            ct,
            ("at", At)));

        Assert.Equal(1L, await host.CountAsync(MeetTestData.Names.SessionTable, ct));
        Assert.Equal(MeetTestData.Names.SessionValues, failure.ConstraintName);
    }

    /// <summary>db-design §4 … §6: the three lookup indexes, two of them on lower-cased expressions.</summary>
    [Theory]
    [InlineData(SessionOrganizerIndex, "lower((organizer_email)::text)")]
    [InlineData(ParticipationEmailIndex, "lower((email)::text)")]
    [InlineData(CourseOwnerIndex, "(owner_google_id)")]
    public async Task TheLookupIndexes_ExistOnTheDesignedExpressions(string index, string expression)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = await IndexDefinitionAsync(host, index, ct);

        Assert.NotNull(definition);
        Assert.Contains(expression, definition, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- audit_event (db-design §9)

    private static Task<int> InsertAuditAsync(
        InstallationTestHost host,
        CancellationToken ct,
        string action,
        string outcome = "succeeded",
        string? targetType = "classroom_participant",
        long? targetId = 41,
        string actorRole = "admin",
        int? counts = 1,
        string? refusal = null) =>
        host.ExecuteAsync(
            """
            INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id, outcome,
                                     refusal_category, request_id, erased_memberships, erased_submissions,
                                     erased_meet_participations, cleared_meet_organizers, cleared_course_owners,
                                     created_at, updated_at)
            VALUES (@at, 'app_user', 7, @role, @action, @targetType, @targetId, @outcome, @refusal, NULL,
                    @counts, @counts, @counts, @counts, @counts, @at, @at)
            """,
            ct,
            ("at", At),
            ("role", actorRole),
            ("action", action),
            ("targetType", targetType),
            ("targetId", targetId),
            ("outcome", outcome),
            ("refusal", refusal),
            ("counts", counts));

    /// <summary>db-design §9: the designed rows are accepted — erasure, lifting and both refusals.</summary>
    [Fact]
    public async Task TheDesignedAuditRows_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAuditAsync(host, ct, ErasureTestData.Codes.ErasedAction, counts: 0);
        await InsertAuditAsync(host, ct, ErasureTestData.Codes.LiftedAction, targetType: "erased_participant", targetId: 300, counts: null);
        await InsertAuditAsync(host, ct, ErasureTestData.Codes.ErasedAction, outcome: "refused", counts: null, refusal: "read_only_mode");
        await InsertAuditAsync(host, ct, ErasureTestData.Codes.LiftedAction, outcome: "refused", targetType: null, targetId: null, counts: null, refusal: "read_only_mode");

        Assert.Equal(4L, await host.ScalarAsync<long>(
            "SELECT count(*) FROM audit_event WHERE action IN ('participant_erased', 'erasure_exclusion_lifted')", ct));
    }

    /// <summary>db-design §9.3: a succeeded erasure without its counts, or another row with them, is rejected.</summary>
    [Theory]
    [InlineData("participant_erased", "classroom_participant", null, AuditErasureCounts)]
    [InlineData("erasure_exclusion_lifted", "erased_participant", 1, AuditErasureCountsAbsent)]
    [InlineData("participant_erased", "classroom_participant", -1, AuditErasureCountsNonNegative)]
    public async Task AnAuditRowWithTheWrongCounts_IsRejected(string action, string targetType, int? counts, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => InsertAuditAsync(host, ct, action, targetType: targetType, counts: counts));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }

    /// <summary>db-design §9.3: an erasure or a lifting by a Dean, or one naming the wrong target, is rejected.</summary>
    [Theory]
    [InlineData("participant_erased", "dean", "classroom_participant", 1, AuditErasureShape)]
    [InlineData("participant_erased", "admin", "app_user", 1, AuditErasureShape)]
    [InlineData("erasure_exclusion_lifted", "dean", "erased_participant", null, AuditLiftShape)]
    [InlineData("erasure_exclusion_lifted", "admin", "classroom_participant", null, AuditLiftShape)]
    public async Task AnAuditRowOfTheWrongShape_IsRejected(string action, string role, string targetType, int? counts, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => InsertAuditAsync(host, ct, action, targetType: targetType, actorRole: role, counts: counts));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }
}
