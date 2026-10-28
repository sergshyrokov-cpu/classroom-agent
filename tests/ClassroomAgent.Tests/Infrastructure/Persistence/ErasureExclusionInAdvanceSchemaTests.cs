using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;
using static ClassroomAgent.Tests.TestInfrastructure.ErasureTestData.Names;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-065 db-design §2, §6, §7 and spec FR-003, FR-007, AC-015 against the migrated database (TC-2): an exclusion may
/// lack the Google user id but never both it and the fingerprint; many exclusions without an id coexist; the trigger
/// <c>tg_erased_participant_fill_only</c> — which the EF model snapshot cannot show (DB_DESIGN N-1) — exists, allows
/// exactly the one fill and rejects every other update; the new audit action and its shape check.
/// </summary>
public sealed class ErasureExclusionInAdvanceSchemaTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset At = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    private static byte[] FingerprintOf(string local) =>
        ErasureTestData.Fingerprint(local + "@school-one.example.test", ErasureTestData.KnownSalt());

    private static Task<long> InsertAsync(
        InstallationTestHost host,
        CancellationToken ct,
        string? googleUserId,
        byte[]? fingerprint) =>
        host.ScalarAsync<long>(
            """
            INSERT INTO erased_participant (google_user_id, email_fingerprint, erased_at, created_at, updated_at)
            VALUES (@id, @fp, @at, @at, @at)
            RETURNING id
            """,
            ct,
            ("id", googleUserId),
            ("fp", fingerprint),
            ("at", At));

    /// <summary>The fill exactly as db-design §4 step 6 writes it.</summary>
    private static Task<int> FillAsync(InstallationTestHost host, CancellationToken ct, long id, string googleUserId) =>
        host.ExecuteAsync(
            "UPDATE erased_participant SET google_user_id = @gid, updated_at = @later WHERE id = @id",
            ct,
            ("gid", googleUserId),
            ("later", At.AddMinutes(5)),
            ("id", id));

    private static async Task<PostgresException> RejectedAsync(Func<Task> write) =>
        await Assert.ThrowsAsync<PostgresException>(write);

    private static Task<string?> GoogleUserIdAsync(InstallationTestHost host, CancellationToken ct, long id) =>
        host.ScalarAsync<string>("SELECT google_user_id FROM erased_participant WHERE id = @id", ct, ("id", id));

    // ---------------------------------------------------------------- columns and checks (db-design §2.1, §2.2)

    /// <summary>FR-003, db-design §2.2: no Google user id with a fingerprint is accepted.</summary>
    [Fact]
    public async Task AnExclusionWithAFingerprint_AndNoGoogleUserId_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var id = await InsertAsync(host, ct, null, FingerprintOf("o.koval"));

        Assert.Null(await GoogleUserIdAsync(host, ct, id));
    }

    /// <summary>FR-003, db-design §2.2: neither the Google user id nor the fingerprint — could never match; rejected.</summary>
    [Fact]
    public async Task AnExclusionWithNeitherIdNorFingerprint_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => InsertAsync(host, ct, null, null));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(ExclusionValues, failure.ConstraintName);
    }

    /// <summary>db-design §2.2: the length rules still hold for an exclusion without an id.</summary>
    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public async Task AnExclusionWithoutId_AndAFingerprintOfAnotherLength_IsRejected(int length)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => InsertAsync(host, ct, null, new byte[length]));

        Assert.Equal(ExclusionValues, failure.ConstraintName);
    }

    /// <summary>db-design §2.4: the unique index lets any number of exclusions without an id coexist.</summary>
    [Fact]
    public async Task ManyExclusionsWithoutId_Coexist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, null, FingerprintOf("a.one"));
        await InsertAsync(host, ct, null, FingerprintOf("b.two"));
        await InsertAsync(host, ct, null, FingerprintOf("c.three"));

        Assert.Equal(3L, await host.CountAsync(ExclusionTable, ct));
    }

    /// <summary>db-design §2.2: the old immutability check is gone — its guarantee moved to the trigger.</summary>
    [Fact]
    public async Task TheImmutabilityCheck_IsReplacedByTheTrigger()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(0L, await host.ScalarAsync<long>(
            "SELECT count(*) FROM pg_constraint WHERE conname = @name", ct, ("name", ExclusionImmutable)));
    }

    // ---------------------------------------------------------------- the trigger (db-design §2.3; DB_DESIGN N-1)

    /// <summary>
    /// DB_DESIGN N-1, db-design §2.3, §7: the trigger exists on <c>erased_participant</c>, enabled, row-level, before update,
    /// calling the designed function — the proof the model snapshot cannot give.
    /// </summary>
    [Fact]
    public async Task TheFillOnlyTrigger_ExistsOnTheTable_BeforeUpdate_ForEachRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var rows = await host.QueryAsync(
            """
            SELECT c.relname, p.proname, t.tgenabled::text, t.tgtype::int
            FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_proc p ON p.oid = t.tgfoid
            WHERE t.tgname = @name AND NOT t.tgisinternal
            """,
            r => (Table: r.GetString(0), Function: r.GetString(1), Enabled: r.GetString(2), Type: r.GetInt32(3)),
            ct,
            ("name", FillOnlyTrigger));

        var trigger = Assert.Single(rows);
        Assert.Equal(ExclusionTable, trigger.Table);
        Assert.Equal(FillOnlyFunction, trigger.Function);
        Assert.Equal("O", trigger.Enabled);
        // tgtype bits (pg_trigger.h): 1 = ROW, 2 = BEFORE, 16 = UPDATE; no INSERT (4) or DELETE (8).
        Assert.Equal(1 | 2 | 16, trigger.Type & (1 | 2 | 4 | 8 | 16));
    }

    /// <summary>FR-007, AC-015, db-design §2.3: an exclusion entered in advance is filled once — accepted.</summary>
    [Fact]
    public async Task TheOneFill_OfAnExclusionWithoutId_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var id = await InsertAsync(host, ct, null, FingerprintOf("o.koval"));

        Assert.Equal(1, await FillAsync(host, ct, id, "100000000000000041"));

        Assert.Equal("100000000000000041", await GoogleUserIdAsync(host, ct, id));
    }

    /// <summary>FR-007, AC-015: once filled, the exclusion is unchangeable — a second id, or only a new stamp, is rejected.</summary>
    [Theory]
    [InlineData("UPDATE erased_participant SET google_user_id = '100000000000000042', updated_at = @later WHERE id = @id")]
    [InlineData("UPDATE erased_participant SET updated_at = @later WHERE id = @id")]
    [InlineData("UPDATE erased_participant SET google_user_id = NULL, updated_at = @later WHERE id = @id")]
    public async Task AFilledExclusion_CannotBeChangedAgain(string sql)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var id = await InsertAsync(host, ct, null, FingerprintOf("o.koval"));
        await FillAsync(host, ct, id, "100000000000000041");

        var failure = await RejectedAsync(() => host.ExecuteAsync(sql, ct, ("later", At.AddHours(1)), ("id", id)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal("100000000000000041", await GoogleUserIdAsync(host, ct, id));
    }

    /// <summary>AC-015, I-1: an exclusion left by an erasure (id at insert) can never be updated — not even its stamp.</summary>
    [Theory]
    [InlineData("UPDATE erased_participant SET google_user_id = '100000000000000042', updated_at = @later WHERE id = @id")]
    [InlineData("UPDATE erased_participant SET updated_at = @later WHERE id = @id")]
    public async Task AnExclusionFromAnErasure_CannotBeUpdated(string sql)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var id = await InsertAsync(host, ct, "100000000000000041", FingerprintOf("o.koval"));

        var failure = await RejectedAsync(() => host.ExecuteAsync(sql, ct, ("later", At.AddHours(1)), ("id", id)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    /// <summary>db-design §2.3: the fill may change nothing but the id and the stamp — each other column is rejected.</summary>
    [Theory]
    [InlineData("email_fingerprint = @otherFingerprint")]
    [InlineData("erased_at = @later")]
    [InlineData("created_at = @later")]
    public async Task AFillThatAlsoChangesAnotherColumn_IsRejected(string change)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var id = await InsertAsync(host, ct, null, FingerprintOf("o.koval"));

        var failure = await RejectedAsync(() => host.ExecuteAsync(
            $"UPDATE erased_participant SET google_user_id = '100000000000000041', updated_at = @later, {change} WHERE id = @id",
            ct,
            ("later", At.AddHours(1)),
            ("otherFingerprint", FingerprintOf("p.melnyk")),
            ("id", id)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Null(await GoogleUserIdAsync(host, ct, id));
    }

    /// <summary>db-design §2.3: an update of an exclusion without id that does not give it an id is rejected.</summary>
    [Fact]
    public async Task AnUpdateOfAnUnmatchedExclusion_ThatDoesNotFillIt_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var id = await InsertAsync(host, ct, null, FingerprintOf("o.koval"));

        var failure = await RejectedAsync(() => host.ExecuteAsync(
            "UPDATE erased_participant SET updated_at = @later WHERE id = @id", ct, ("later", At.AddHours(1)), ("id", id)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    /// <summary>I-2, db-design §2.4: the fill cannot take a Google id another exclusion already holds.</summary>
    [Fact]
    public async Task TheFill_CannotTakeAnIdHeldByAnotherExclusion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertAsync(host, ct, "100000000000000041", FingerprintOf("old.address"));
        var id = await InsertAsync(host, ct, null, FingerprintOf("o.koval"));

        var failure = await RejectedAsync(() => FillAsync(host, ct, id, "100000000000000041"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Equal(ExclusionGoogleUserIdUnique, failure.ConstraintName);
    }

    /// <summary>FR-010: unmatched and filled exclusions can still be deleted (the lifting).</summary>
    [Fact]
    public async Task UnmatchedAndFilledExclusions_CanBeDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var unmatched = await InsertAsync(host, ct, null, FingerprintOf("a.one"));
        var filled = await InsertAsync(host, ct, null, FingerprintOf("b.two"));
        await FillAsync(host, ct, filled, "100000000000000041");

        await host.ExecuteAsync("DELETE FROM erased_participant WHERE id IN (@a, @b)", ct, ("a", unmatched), ("b", filled));

        Assert.Equal(0L, await host.CountAsync(ExclusionTable, ct));
    }

    // ---------------------------------------------------------------- audit_event (db-design §6)

    private static Task<int> InsertAuditAsync(
        InstallationTestHost host,
        CancellationToken ct,
        string outcome = "succeeded",
        string? targetType = "erased_participant",
        long? targetId = 300,
        string actorRole = "admin",
        string? refusal = null) =>
        host.ExecuteAsync(
            """
            INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id, outcome,
                                     refusal_category, request_id, created_at, updated_at)
            VALUES (@at, 'app_user', 7, @role, @action, @targetType, @targetId, @outcome, @refusal, NULL, @at, @at)
            """,
            ct,
            ("at", At),
            ("role", actorRole),
            ("action", ErasureTestData.Codes.EnteredAction),
            ("targetType", targetType),
            ("targetId", targetId),
            ("outcome", outcome),
            ("refusal", refusal));

    /// <summary>db-design §6.1, §6.3: the succeeded entry naming its exclusion and the read-only refusal naming nothing.</summary>
    [Fact]
    public async Task TheDesignedEntryAuditRows_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAuditAsync(host, ct);
        await InsertAuditAsync(host, ct, outcome: "refused", targetType: null, targetId: null, refusal: "read_only_mode");

        Assert.Equal(2L, await host.ScalarAsync<long>(
            "SELECT count(*) FROM audit_event WHERE action = @action", ct, ("action", ErasureTestData.Codes.EnteredAction)));
    }

    /// <summary>db-design §6.2: an entry by a Dean, naming another target type, or succeeded without a target, is rejected.</summary>
    [Theory]
    [InlineData("dean", "erased_participant", 300L)]
    [InlineData("admin", "classroom_participant", 300L)]
    [InlineData("admin", null, null)]
    public async Task AnEntryAuditRowOfTheWrongShape_IsRejected(string role, string? targetType, long? targetId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => InsertAuditAsync(host, ct, targetType: targetType, targetId: targetId, actorRole: role));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditEntryShape, failure.ConstraintName);
    }

    /// <summary>db-design §6.2: the US-038 count checks keep the erasure counts off an entry row.</summary>
    [Fact]
    public async Task AnEntryAuditRowWithErasureCounts_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await RejectedAsync(() => host.ExecuteAsync(
            """
            INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id, outcome,
                                     erased_memberships, created_at, updated_at)
            VALUES (@at, 'app_user', 7, 'admin', @action, 'erased_participant', 300, 'succeeded', 1, @at, @at)
            """,
            ct,
            ("at", At),
            ("action", ErasureTestData.Codes.EnteredAction)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(AuditErasureCountsAbsent, failure.ConstraintName);
    }

    // ---------------------------------------------------------------- the migration (db-design §7, PC-2)

    /// <summary>PC-2, db-design §7: the change ships as the designed migration.</summary>
    [Fact]
    public async Task TheMigration_IsApplied()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var migrations = await host.QueryAsync(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            r => r.GetString(0),
            ct);

        Assert.Single(migrations, m => m.EndsWith(InAdvanceMigration, StringComparison.Ordinal));
    }
}
