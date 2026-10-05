using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-014 db-design §4: the migration creates <c>classroom_participant</c> with the Google <c>userId</c> as the
/// person's only identity, an optional address and no role column. Against real PostgreSQL via Testcontainers
/// (TC-2).
/// </summary>
public sealed class ClassroomParticipantSchemaTests(PostgreSqlFixture database)
{
    /// <summary>db-design §4.1 and US-042 db-design §2: eight columns, with the nullability and bounds the designs state.</summary>
    [Fact]
    public async Task TheMigration_CreatesTheTable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT column_name, is_nullable, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'classroom_participant'
            ORDER BY column_name
            """,
            r => (Name: r.GetString(0), Nullable: r.GetString(1), Length: r.IsDBNull(2) ? (int?)null : r.GetInt32(2)),
            ct);

        Assert.Equal(8, columns.Count); // US-042 db-design §2 adds surname and given_name
        Assert.Contains(columns, c => c.Name == "id" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "google_user_id" && c.Nullable == "NO" && c.Length == 64);
        Assert.Contains(columns, c => c.Name == "email" && c.Nullable == "YES" && c.Length == 320);
        Assert.Contains(columns, c => c.Name == "full_name" && c.Nullable == "YES" && c.Length == 750);
        Assert.Contains(columns, c => c.Name == "surname" && c.Nullable == "YES" && c.Length == 750);
        Assert.Contains(columns, c => c.Name == "given_name" && c.Nullable == "YES" && c.Length == 750);
        Assert.Contains(columns, c => c.Name == "created_at" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "updated_at" && c.Nullable == "NO");
    }

    /// <summary>
    /// db-design §4.3, BR-050: the person carries no Classroom role — it lives on the membership. A role column
    /// here would be a modelling defect, so its absence is asserted rather than assumed.
    /// </summary>
    [Fact]
    public async Task TheTable_HasNoRoleColumn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'classroom_participant'
            """,
            r => r.GetString(0),
            ct);

        // The table must exist for the absence below to mean anything: on a database without it the query
        // returns nothing and every DoesNotContain would pass vacuously.
        Assert.Contains("google_user_id", columns);
        Assert.DoesNotContain("role", columns);
        Assert.DoesNotContain("classroom_role", columns);
    }

    /// <summary>db-design §4.2, PC-3: the Google user id is unique — it is the upsert key and the identity.</summary>
    [Fact]
    public async Task ADuplicateGoogleUserId_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await CourseRows.InsertParticipantAsync(host, ct, googleUserId: CourseTestData.UserId(1));

        var second = async () => await CourseRows.InsertParticipantAsync(
            host,
            ct,
            googleUserId: CourseTestData.UserId(1),
            email: CourseTestData.Email("second"));

        var error = await Assert.ThrowsAsync<PostgresException>(second);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal(CourseTestData.Constraints.ParticipantGoogleUserId, error.ConstraintName);
    }

    /// <summary>
    /// db-design §4.2, OD-011: two people may hold the same address — this must <b>succeed</b>. Google permits a
    /// deleted account's address to be reused by a new <c>userId</c>, and a unique index would turn that into a
    /// failed import for the whole school. This test fails the moment someone adds <c>IsUnique()</c>.
    /// </summary>
    [Fact]
    public async Task TwoParticipantsSharingAnEmailAddress_AreBothStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var shared = CourseTestData.Email("reused.address");

        await CourseRows.InsertParticipantAsync(host, ct, googleUserId: CourseTestData.UserId(1), email: shared);
        await CourseRows.InsertParticipantAsync(host, ct, googleUserId: CourseTestData.UserId(2), email: shared);

        var count = await host.ScalarAsync<long>(
            "SELECT count(*) FROM classroom_participant WHERE email = @email",
            ct,
            ("email", shared));
        Assert.Equal(2, count);
    }

    /// <summary>db-design §4.2: the address is indexed, because Epic 4 matches a Meet participant by it (PC-12).</summary>
    [Fact]
    public async Task TheAddress_IsIndexedButNotUnique()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var indexes = await host.QueryAsync(
            """
            SELECT i.relname::text, ix.indisunique
            FROM pg_index ix
            JOIN pg_class i ON i.oid = ix.indexrelid
            WHERE ix.indrelid = 'classroom_participant'::regclass
            """,
            r => (Name: r.GetString(0), Unique: r.GetBoolean(1)),
            ct);

        Assert.Contains(indexes, i => i.Name == CourseTestData.Constraints.ParticipantEmail && !i.Unique);
        Assert.Contains(indexes, i => i.Name == CourseTestData.Constraints.ParticipantGoogleUserId && i.Unique);
    }

    /// <summary>
    /// db-design §4.1, OD-006: a roster entry Classroom returns without an address is stored by its Google id.
    /// Several such rows coexist, because a unique index permits many nulls in PostgreSQL — and here there is no
    /// unique index on the address at all.
    /// </summary>
    [Fact]
    public async Task ParticipantsWithNoAddress_AreStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await CourseRows.InsertParticipantAsync(host, ct, googleUserId: CourseTestData.UserId(1), email: null);
        await CourseRows.InsertParticipantAsync(host, ct, googleUserId: CourseTestData.UserId(2), email: null);

        var count = await host.ScalarAsync<long>(
            "SELECT count(*) FROM classroom_participant WHERE email IS NULL",
            ct);
        Assert.Equal(2, count);
    }

    /// <summary>db-design §4.1: the table has no check constraint and no foreign key.</summary>
    [Fact]
    public async Task TheTable_HasNoCheckConstraintAndNoForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var checks = await InstallationSchemaQueries.CheckConstraintsAsync(host, CourseTestData.ParticipantTable, ct);
        var keys = await InstallationSchemaQueries.ForeignKeysAsync(host, CourseTestData.ParticipantTable, ct);

        Assert.Empty(checks);
        Assert.Empty(keys);
    }
}
