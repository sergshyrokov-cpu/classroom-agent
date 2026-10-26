using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-058 db-design §2 … §5, entity model §1 … §4: the migration <c>AddSyncDataHealthRecords</c> adds the mark column to
/// <c>course_work</c>, the last-read column to <c>course</c> and the table <c>sync_skipped_course</c> with its key, unique
/// index and checks; the real repository stores, reads and removes a record, and the reason travels as its code —
/// against real PostgreSQL (TC-2).
/// </summary>
public sealed class SyncDataHealthPersistenceTests(PostgreSqlFixture database)
{
    private const string Table = "sync_skipped_course";

    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A writable host whose own scheduled run is skipped (no saved connection) — the IncrementalSyncPersistenceTests precedent.</summary>
    private Task<InstallationTestHost> StartWritableAsync(CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, connection: SeededConnection.None);

    private static async Task<(string Type, string Nullable, string? Default)> ColumnAsync(
        InstallationTestHost host,
        string table,
        string column,
        CancellationToken ct) =>
        Assert.Single(await host.QueryAsync(
            """
            SELECT data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND column_name = @column
            """,
            r => (r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)),
            ct,
            ("table", table),
            ("column", column)));

    /// <summary>db-design §2: <c>course_work.not_in_classroom_since</c> is a nullable timestamptz with no default.</summary>
    [Fact]
    public async Task TheMarkColumn_IsANullableTimestamptzWithNoDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(("timestamp with time zone", "YES", (string?)null), await ColumnAsync(host, "course_work", "not_in_classroom_since", ct));
    }

    /// <summary>db-design §3: <c>course.last_complete_read_at</c> is a nullable timestamptz with no default.</summary>
    [Fact]
    public async Task TheLastReadColumn_IsANullableTimestamptzWithNoDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(("timestamp with time zone", "YES", (string?)null), await ColumnAsync(host, "course", "last_complete_read_at", ct));
    }

    /// <summary>db-design §4: the table's columns, exactly.</summary>
    [Fact]
    public async Task TheTable_HasExactlyTheDesignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(
            new[]
            {
                "course_google_id character varying 64 NO -",
                "course_name character varying 750 YES -",
                "created_at timestamp with time zone - NO -",
                "id bigint - NO -",
                "reason character varying 32 NO -",
                "skipped_since timestamp with time zone - NO -",
                "updated_at timestamp with time zone - NO -",
            },
            await InstallationSchemaQueries.ColumnsAsync(host, Table, ct));
    }

    /// <summary>db-design §4: the primary key, the one unique index, the three checks and no foreign key.</summary>
    [Fact]
    public async Task TheTable_HasTheDesignedKeysIndexesAndChecks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal("pk_sync_skipped_course", await InstallationSchemaQueries.PrimaryKeyAsync(host, Table, ct));
        Assert.Equal(
            new[] { "pk_sync_skipped_course", "uq_sync_skipped_course_course_google_id" },
            await InstallationSchemaQueries.IndexNamesAsync(host, Table, ct));
        Assert.Equal(
            new[] { "ck_sync_skipped_course_google_id", "ck_sync_skipped_course_name", "ck_sync_skipped_course_reason" },
            await InstallationSchemaQueries.CheckConstraintsAsync(host, Table, ct));
        Assert.Empty(await InstallationSchemaQueries.ForeignKeysAsync(host, Table, ct));
    }

    private static Task<int> InsertAsync(
        InstallationTestHost host,
        CancellationToken ct,
        string googleId = "100000000001",
        string? name = "Test Course",
        string reason = "value_out_of_range") =>
        host.ExecuteAsync(
            $"""
            INSERT INTO {Table} (course_google_id, course_name, reason, skipped_since, created_at, updated_at)
            VALUES (@id, @name, @reason, @at, @at, @at)
            """,
            ct,
            ("id", googleId),
            ("name", name),
            ("reason", reason),
            ("at", T));

    /// <summary>db-design §4: both reason codes and a null name are accepted.</summary>
    [Fact]
    public async Task BothReasonCodes_AndNoName_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, "100000000001", null, "value_out_of_range");
        await InsertAsync(host, ct, "100000000002", "Test Course", "unknown_course_state");

        Assert.Equal(2, await host.CountAsync(Table, ct));
    }

    /// <summary>db-design §4, VR-001 … VR-003: each check and the unique index rejects what it exists to reject.</summary>
    [Theory]
    [InlineData("ck_sync_skipped_course_reason", "100000000001", "Test Course", "ValueOutOfRange")]
    [InlineData("ck_sync_skipped_course_google_id", "", "Test Course", "value_out_of_range")]
    [InlineData("ck_sync_skipped_course_name", "100000000001", "", "value_out_of_range")]
    public async Task AViolatingRow_IsRejectedByItsCheck(string constraint, string googleId, string name, string reason)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, ct, googleId, name, reason));

        Assert.Equal(constraint, failure.ConstraintName);
    }

    /// <summary>db-design §4, FR-005.3: at most one record per course.</summary>
    [Fact]
    public async Task ASecondRecordForOneCourse_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await InsertAsync(host, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, ct, reason: "unknown_course_state"));

        Assert.Equal("uq_sync_skipped_course_course_google_id", failure.ConstraintName);
    }

    // ---- ISyncSkippedCourseRepository (entity model §4) ------------------------------------------------------------

    private static async Task AddAsync(InstallationTestHost host, SyncSkippedCourse record, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISyncSkippedCourseRepository>().Add(record);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }

    /// <summary>
    /// Entity model §3, §4: a record written through EF Core is stored with its reason as the lower-case code and the
    /// interceptor's timestamps, and reads back through the repository as it was written.
    /// </summary>
    [Fact]
    public async Task ARecord_RoundTripsThroughTheRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await AddAsync(host, SyncSkippedCourse.Record("100000000001", "Test Course", CourseSkipReason.UnknownCourseState, T), ct);
        await AddAsync(host, SyncSkippedCourse.Record("100000000002", null, CourseSkipReason.ValueOutOfRange, T.AddHours(1)), ct);

        Assert.Equal(
            new[] { "100000000001|unknown_course_state", "100000000002|value_out_of_range" },
            await host.QueryAsync(
                $"SELECT course_google_id || '|' || reason FROM {Table} WHERE created_at IS NOT NULL AND updated_at IS NOT NULL ORDER BY course_google_id",
                r => r.GetString(0),
                ct));

        using var scope = host.CreateScope();
        var all = await scope.ServiceProvider.GetRequiredService<ISyncSkippedCourseRepository>().GetAllAsync(ct);
        var first = Assert.Single(all, r => r.CourseGoogleId == "100000000001");
        Assert.Equal("Test Course", first.CourseName);
        Assert.Equal(CourseSkipReason.UnknownCourseState, first.Reason);
        Assert.Equal(T, first.SkippedSince);
        var second = Assert.Single(all, r => r.CourseGoogleId == "100000000002");
        Assert.Null(second.CourseName);
        Assert.Equal(CourseSkipReason.ValueOutOfRange, second.Reason);
        Assert.Equal(T.AddHours(1), second.SkippedSince);
    }

    /// <summary>Entity model §4: records are read tracked, so a repeated skip and a removal are saved by the unit of work.</summary>
    [Fact]
    public async Task ARecordReadFromTheRepository_CanBeChangedAndRemoved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await AddAsync(host, SyncSkippedCourse.Record("100000000001", "First", CourseSkipReason.UnknownCourseState, T), ct);
        await AddAsync(host, SyncSkippedCourse.Record("100000000002", "Second", CourseSkipReason.UnknownCourseState, T), ct);

        using (var scope = host.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ISyncSkippedCourseRepository>();
            var all = await repository.GetAllAsync(ct);
            all.Single(r => r.CourseGoogleId == "100000000001").SkipAgain("Renamed", CourseSkipReason.ValueOutOfRange);
            repository.Remove(all.Single(r => r.CourseGoogleId == "100000000002"));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        Assert.Equal(
            new[] { "100000000001|Renamed|value_out_of_range" },
            await host.QueryAsync(
                $"SELECT course_google_id || '|' || course_name || '|' || reason FROM {Table}",
                r => r.GetString(0),
                ct));
    }

    /// <summary>
    /// Entity model §1, §2: the mark and the last-read instant written through EF Core are stored in their columns.
    /// </summary>
    [Fact]
    public async Task TheMarkAndTheLastReadInstant_AreStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        using (var scope = host.CreateScope())
        {
            var course = Course.Import(
                CourseTestData.CourseId(1),
                CourseState.Active,
                new CourseDetails(CourseTestData.CourseName(1), null, null, null, null, null, null, T, null, null, null, null));
            course.RecordCompleteRead(T.AddHours(2));
            scope.ServiceProvider.GetRequiredService<ICourseRepository>().Add(course);
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await work.SaveChangesAsync(ct);
            var item = CourseWork.Import(
                course.Id,
                CourseWorkTestData.ItemId(1),
                CourseWorkResource.CourseWork,
                new CourseWorkDetails(CourseWorkTestData.Title(1), T, null, null, T, T));
            item.MarkNotInClassroom(T.AddHours(3));
            scope.ServiceProvider.GetRequiredService<ICourseWorkRepository>().Add(item);
            await work.SaveChangesAsync(ct);
        }

        Assert.Equal(
            [T.AddHours(2)],
            await host.QueryAsync("SELECT last_complete_read_at FROM course", r => r.GetFieldValue<DateTimeOffset>(0), ct));
        Assert.Equal(
            [T.AddHours(3)],
            await host.QueryAsync("SELECT not_in_classroom_since FROM course_work", r => r.GetFieldValue<DateTimeOffset>(0), ct));
    }
}
