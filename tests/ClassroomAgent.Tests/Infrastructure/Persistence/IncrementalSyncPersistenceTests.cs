using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-018 db-design §2–§6, entity model §4: the migration <c>AddIncrementalSync</c> adds the two nullable instants
/// and the check that ties the full-run time to the last successful run, and the real repository reads the remembered
/// value untracked — against real PostgreSQL (TC-2). The value written through EF Core and read back is the one the
/// rule compares at storage precision (VR-001, I-4).
/// </summary>
public sealed class IncrementalSyncPersistenceTests(PostgreSqlFixture database)
{
    private const string FullRunCheck = "ck_sync_state_last_full_run_at";

    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A writable host whose own scheduled run is skipped (no saved connection) — the MeetSessionRepositoryTests precedent.</summary>
    private Task<InstallationTestHost> StartWritableAsync(CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, connection: SeededConnection.None);

    private static async Task<IReadOnlyList<(string Type, string Nullable, string? Default)>> ColumnAsync(
        InstallationTestHost host,
        string table,
        string column,
        CancellationToken ct) =>
        await host.QueryAsync(
            """
            SELECT data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND column_name = @column
            """,
            r => (r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)),
            ct,
            ("table", table),
            ("column", column));

    /// <summary>db-design §2: <c>course.complete_read_update_time</c> is a nullable timestamptz with no default.</summary>
    [Fact]
    public async Task TheCourseColumn_IsANullableTimestamptzWithNoDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var column = Assert.Single(await ColumnAsync(host, "course", "complete_read_update_time", ct));

        Assert.Equal("timestamp with time zone", column.Type);
        Assert.Equal("YES", column.Nullable);
        Assert.Null(column.Default);
    }

    /// <summary>db-design §3: <c>sync_state.last_full_run_at</c> is a nullable timestamptz with no default.</summary>
    [Fact]
    public async Task TheSyncStateColumn_IsANullableTimestamptzWithNoDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var column = Assert.Single(await ColumnAsync(host, "sync_state", "last_full_run_at", ct));

        Assert.Equal("timestamp with time zone", column.Type);
        Assert.Equal("YES", column.Nullable);
        Assert.Null(column.Default);
    }

    /// <summary>db-design §2: no index is added to <c>course</c> — the lookup is served by <c>uq_course_google_id</c>.</summary>
    [Fact]
    public async Task NoIndexIsAdded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var indexes = await InstallationSchemaQueries.IndexNamesAsync(host, CourseTestData.CourseTable, ct);

        Assert.DoesNotContain(indexes, i => i.Contains("complete_read", StringComparison.Ordinal));
        Assert.DoesNotContain(
            await InstallationSchemaQueries.IndexNamesAsync(host, SyncTestData.Table, ct),
            i => i.Contains("full_run", StringComparison.Ordinal));
    }

    private static Task<int> InsertCompletedStateAsync(
        InstallationTestHost host,
        DateTimeOffset? lastSuccessfulRunAt,
        DateTimeOffset? lastFullRunAt,
        CancellationToken ct)
    {
        var started = T;
        var finished = T.AddMinutes(10);
        return host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_error, last_successful_run_at, last_full_run_at, created_at, updated_at)
            VALUES (true, @status, @runId, @startedAt, @finishedAt, 0, @lastError,
                    @lastSuccessfulRunAt, @lastFullRunAt, @stamp, @stamp)
            """,
            ct,
            ("status", lastSuccessfulRunAt is null ? SyncTestData.Status.Failed : SyncTestData.Status.Completed),
            ("runId", Guid.NewGuid()),
            ("startedAt", started),
            ("finishedAt", finished),
            ("lastError", lastSuccessfulRunAt is null ? "GoogleUnavailable" : null),
            ("lastSuccessfulRunAt", lastSuccessfulRunAt),
            ("lastFullRunAt", lastFullRunAt),
            ("stamp", host.Time.GetUtcNow()));
    }

    /// <summary>db-design §3: the check constraint exists under its name.</summary>
    [Fact]
    public async Task TheFullRunCheck_Exists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Contains(FullRunCheck, await InstallationSchemaQueries.CheckConstraintsAsync(host, SyncTestData.Table, ct));
    }

    /// <summary>db-design §3: a full-run time equal to or earlier than the last successful run is accepted, and so is null.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-60)]
    public async Task AFullRunTimeNotLaterThanTheLastSuccess_IsAccepted(int? minutesFromSuccess)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var success = T.AddMinutes(10);

        var rows = await InsertCompletedStateAsync(
            host,
            success,
            minutesFromSuccess is { } m ? success.AddMinutes(m) : null,
            ct);

        Assert.Equal(1, rows);
    }

    /// <summary>db-design §3: a full-run time later than the last successful run is rejected by the check.</summary>
    [Fact]
    public async Task AFullRunTimeLaterThanTheLastSuccess_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var success = T.AddMinutes(10);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => InsertCompletedStateAsync(host, success, success.AddTicks(10), ct));

        Assert.Equal("23514", error.SqlState);
        Assert.Equal(FullRunCheck, error.ConstraintName);
    }

    /// <summary>db-design §3: a full-run time with no successful run at all is rejected by the check.</summary>
    [Fact]
    public async Task AFullRunTimeWithoutASuccessfulRun_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => InsertCompletedStateAsync(host, null, T, ct));

        Assert.Equal("23514", error.SqlState);
        Assert.Equal(FullRunCheck, error.ConstraintName);
    }

    /// <summary>db-design §4: no backfill — a course inserted the way pre-Story code did has no remembered value.</summary>
    [Fact]
    public async Task AnExistingCourse_HasNoRememberedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1), state: CourseTestData.States.Archived, updateTime: T);

        var value = await host.ScalarAsync<object>(
            "SELECT complete_read_update_time FROM course WHERE google_id = @id",
            ct,
            ("id", CourseTestData.CourseId(1)));

        Assert.Null(value);
    }

    // ---- ICourseRepository.GetCompleteReadUpdateTimeAsync (entity model §4, db-design §6) ----------------------------

    private static CourseDetails Details(DateTimeOffset? updateTime) =>
        new(CourseTestData.CourseName(1), null, null, null, null, null, null, updateTime, null, null, null, null);

    private static async Task SaveCourseAsync(InstallationTestHost host, DateTimeOffset? updateTime, bool recordCompleteRead, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        var course = Course.Import(CourseTestData.CourseId(1), CourseState.Archived, Details(updateTime));
        if (recordCompleteRead)
        {
            course.RecordCompleteRead();
        }

        scope.ServiceProvider.GetRequiredService<ICourseRepository>().Add(course);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }

    private static async Task<DateTimeOffset?> LookUpAsync(InstallationTestHost host, string googleId, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICourseRepository>().GetCompleteReadUpdateTimeAsync(googleId, ct);
    }

    /// <summary>db-design §6, FR-001 (c): an unknown course has no remembered value.</summary>
    [Fact]
    public async Task AnUnknownCourse_HasNoRememberedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);

        Assert.Null(await LookUpAsync(host, CourseTestData.CourseId(9), ct));
    }

    /// <summary>db-design §6: a stored course that was never completely read has no remembered value.</summary>
    [Fact]
    public async Task ACourseNeverCompletelyRead_HasNoRememberedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await SaveCourseAsync(host, T, recordCompleteRead: false, ct);

        Assert.Null(await LookUpAsync(host, CourseTestData.CourseId(1), ct));
    }

    /// <summary>
    /// FR-002, VR-001, I-4, db-design §5: the value written through EF Core reads back exactly as the in-memory
    /// value — Google's finer fraction was truncated before storing, so both sides of the comparison agree.
    /// </summary>
    [Fact]
    public async Task TheRememberedValue_RoundTripsAtStoragePrecision()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await SaveCourseAsync(host, T.AddTicks(10 * 123 + 7), recordCompleteRead: true, ct);

        var stored = await LookUpAsync(host, CourseTestData.CourseId(1), ct);

        Assert.Equal(T.AddTicks(10 * 123), stored);
    }

    /// <summary>entity model §4: the lookup is untracked — it attaches no course to the unit of work.</summary>
    [Fact]
    public async Task TheLookup_AttachesNoEntity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await SaveCourseAsync(host, T, recordCompleteRead: true, ct);

        using var scope = host.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICourseRepository>()
            .GetCompleteReadUpdateTimeAsync(CourseTestData.CourseId(1), ct);

        var context = scope.ServiceProvider.GetRequiredService<ClassroomAgent.Infrastructure.Persistence.ClassroomAgentDbContext>();
        Assert.Empty(context.ChangeTracker.Entries<Course>());
    }
}
