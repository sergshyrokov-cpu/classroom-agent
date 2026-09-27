using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-014 db-design §3: the migration creates <c>course</c> with exactly the columns the design fixes, and the
/// database — not application code alone — enforces the unique Google id and the five-value state vocabulary.
/// Against real PostgreSQL via Testcontainers; the InMemory provider enforces none of this (TC-2).
/// </summary>
public sealed class CourseSchemaTests(PostgreSqlFixture database)
{
    /// <summary>db-design §3.1: seventeen columns, with the nullability and bounds the design states.</summary>
    [Fact]
    public async Task TheMigration_CreatesTheTable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT column_name, is_nullable, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'course'
            ORDER BY column_name
            """,
            r => (Name: r.GetString(0), Nullable: r.GetString(1), Length: r.IsDBNull(2) ? (int?)null : r.GetInt32(2)),
            ct);

        Assert.Equal(17, columns.Count);
        Assert.Contains(columns, c => c.Name == "id" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "google_id" && c.Nullable == "NO" && c.Length == 64);
        Assert.Contains(columns, c => c.Name == "name" && c.Nullable == "NO" && c.Length == 750);
        Assert.Contains(columns, c => c.Name == "section" && c.Nullable == "YES" && c.Length == 2800);
        Assert.Contains(columns, c => c.Name == "description_heading" && c.Nullable == "YES" && c.Length == 3600);
        Assert.Contains(columns, c => c.Name == "description" && c.Nullable == "YES" && c.Length == 30000);
        Assert.Contains(columns, c => c.Name == "room" && c.Nullable == "YES" && c.Length == 650);
        Assert.Contains(columns, c => c.Name == "owner_google_id" && c.Nullable == "YES" && c.Length == 64);
        Assert.Contains(columns, c => c.Name == "creation_time" && c.Nullable == "YES");
        Assert.Contains(columns, c => c.Name == "update_time" && c.Nullable == "YES");
        Assert.Contains(columns, c => c.Name == "course_state" && c.Nullable == "NO" && c.Length == 16);
        Assert.Contains(columns, c => c.Name == "alternate_link" && c.Nullable == "YES" && c.Length == 2048);
        Assert.Contains(columns, c => c.Name == "teacher_folder_id" && c.Nullable == "YES" && c.Length == 128);
        Assert.Contains(columns, c => c.Name == "teacher_folder_title" && c.Nullable == "YES" && c.Length == 750);
        Assert.Contains(columns, c => c.Name == "calendar_id" && c.Nullable == "YES" && c.Length == 256);
        Assert.Contains(columns, c => c.Name == "created_at" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "updated_at" && c.Nullable == "NO");
    }

    /// <summary>db-design §3.1, §3.4: the primary key and the one unique index exist, and nothing else does.</summary>
    [Fact]
    public async Task TheOnlyIndexes_AreThePrimaryKeyAndTheGoogleId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var primaryKey = await InstallationSchemaQueries.PrimaryKeyAsync(host, CourseTestData.CourseTable, ct);
        var indexes = await InstallationSchemaQueries.IndexNamesAsync(host, CourseTestData.CourseTable, ct);

        Assert.Equal(CourseTestData.Constraints.CoursePrimaryKey, primaryKey);
        Assert.Equal(
            new[] { CourseTestData.Constraints.CoursePrimaryKey, CourseTestData.Constraints.CourseGoogleId },
            indexes);
    }

    /// <summary>db-design §3.1, spec FR-008: the Google id is the upsert key, so a duplicate is rejected.</summary>
    [Fact]
    public async Task ADuplicateGoogleId_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));

        var second = async () => await CourseRows.InsertCourseAsync(
            host,
            ct,
            googleId: CourseTestData.CourseId(1),
            name: CourseTestData.CourseName(2));

        var error = await Assert.ThrowsAsync<PostgresException>(second);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal(CourseTestData.Constraints.CourseGoogleId, error.ConstraintName);
    }

    /// <summary>db-design §3.2: each of the five §3 states is storable.</summary>
    [Theory]
    [InlineData(CourseTestData.States.Active)]
    [InlineData(CourseTestData.States.Archived)]
    [InlineData(CourseTestData.States.Provisioned)]
    [InlineData(CourseTestData.States.Declined)]
    [InlineData(CourseTestData.States.Suspended)]
    public async Task EveryStateOfTheVocabulary_IsAccepted(string state)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var id = await CourseRows.InsertCourseAsync(host, ct, state: state);

        Assert.True(id > 0);
    }

    /// <summary>
    /// db-design §3.2, OD-010: a state outside the five is rejected by the database. The Application half of the
    /// same decision — skipping such a course while the run completes — is proved separately, and this constraint
    /// should never be what stops it.
    /// </summary>
    [Fact]
    public async Task AStateOutsideTheVocabulary_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var insert = async () => await CourseRows.InsertCourseAsync(host, ct, state: "unspecified");

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseTestData.Constraints.CourseState, error.ConstraintName);
    }

    /// <summary>
    /// db-design §3.3: the owner and both Google instants are nullable on purpose — a column no requirement reads
    /// must not be able to fail a school's import. A later <c>NOT NULL</c> would break imports.
    /// </summary>
    [Fact]
    public async Task ACourseWithNoOwnerAndNoGoogleInstants_IsStorable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var id = await CourseRows.InsertCourseAsync(
            host,
            ct,
            ownerGoogleId: null,
            creationTime: null,
            updateTime: null);

        var stored = await host.QueryAsync(
            "SELECT owner_google_id, creation_time, update_time FROM course WHERE id = @id",
            r => (Owner: r.IsDBNull(0) ? null : r.GetString(0), HasCreation: !r.IsDBNull(1), HasUpdate: !r.IsDBNull(2)),
            ct,
            ("id", id));

        var row = Assert.Single(stored);
        Assert.Null(row.Owner);
        Assert.False(row.HasCreation);
        Assert.False(row.HasUpdate);
    }

    /// <summary>db-design §3.4: the course has no foreign key — the owner is a value, not a reference (OD-004).</summary>
    [Fact]
    public async Task TheTable_HasNoForeignKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var keys = await InstallationSchemaQueries.ForeignKeysAsync(host, CourseTestData.CourseTable, ct);

        Assert.Empty(keys);
    }

    /// <summary>db-design §3.2: the state vocabulary is the table's only check constraint.</summary>
    [Fact]
    public async Task TheOnlyCheckConstraint_IsTheStateVocabulary()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var constraints = await InstallationSchemaQueries.CheckConstraintsAsync(host, CourseTestData.CourseTable, ct);

        Assert.Equal(new[] { CourseTestData.Constraints.CourseState }, constraints);
    }
}
