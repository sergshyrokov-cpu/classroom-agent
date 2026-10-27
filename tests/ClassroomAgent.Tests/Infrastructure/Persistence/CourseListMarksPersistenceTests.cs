using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-059 db-design §2 … §4, §7, entity model §1 … §6: the migration <c>AddCourseListMarks</c> adds the mark, its
/// instant and the last sighting to <c>course</c> with the check <c>ck_course_absence_mark</c>, the account-change
/// instant to <c>workspace_connection</c> and the empty-list flag to <c>sync_state</c>; EF Core stores the mark as its
/// lower-case code, the repository loads every course tracked, and the three entities' new values round-trip — against
/// real PostgreSQL (TC-2).
/// </summary>
public sealed class CourseListMarksPersistenceTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset T = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A writable host whose own scheduled run is skipped (no saved connection) — the SyncDataHealthPersistenceTests precedent.</summary>
    private Task<InstallationTestHost> StartWritableAsync(CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, connection: SeededConnection.None);

    private static async Task<(string Type, string Nullable, string? Default, int? Length)> ColumnAsync(
        InstallationTestHost host,
        string table,
        string column,
        CancellationToken ct) =>
        Assert.Single(await host.QueryAsync(
            """
            SELECT data_type, is_nullable, column_default, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND column_name = @column
            """,
            r => (r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? (int?)null : r.GetInt32(3)),
            ct,
            ("table", table),
            ("column", column)));

    // ---- Schema (db-design §2 … §4, §7) ------------------------------------------------------------------------------

    /// <summary>db-design §2: the three course columns — nullable, no default; the mark a varchar(40).</summary>
    [Fact]
    public async Task TheCourseColumns_AreAsDesigned()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(("character varying", "YES", (string?)null, (int?)40), await ColumnAsync(host, "course", "absence_mark", ct));
        Assert.Equal(("timestamp with time zone", "YES", (string?)null, (int?)null), await ColumnAsync(host, "course", "absent_since", ct));
        Assert.Equal(("timestamp with time zone", "YES", (string?)null, (int?)null), await ColumnAsync(host, "course", "last_listed_at", ct));
    }

    /// <summary>db-design §3: <c>workspace_connection.technical_account_changed_at</c> is a nullable timestamptz with no default.</summary>
    [Fact]
    public async Task TheChangeInstantColumn_IsANullableTimestamptzWithNoDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(
            ("timestamp with time zone", "YES", (string?)null, (int?)null),
            await ColumnAsync(host, "workspace_connection", "technical_account_changed_at", ct));
    }

    /// <summary>db-design §4: <c>sync_state.last_course_list_empty</c> is a boolean, NOT NULL, default false.</summary>
    [Fact]
    public async Task TheFlagColumn_IsANotNullBooleanDefaultingToFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(("boolean", "NO", "false", (int?)null), await ColumnAsync(host, "sync_state", "last_course_list_empty", ct));
    }

    /// <summary>db-design §2, VR-003: no mark, and each kind with its instant, are accepted.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("not_in_classroom", true)]
    [InlineData("not_visible_to_technical_account", true)]
    public async Task AValidMark_IsAccepted(string? mark, bool withInstant)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));

        await SetMarkAsync(host, course, mark, withInstant ? T : null, ct);

        Assert.Equal(1, await host.CountAsync("course", ct));
    }

    /// <summary>db-design §2, VR-003: a kind without an instant, an instant without a kind and an unknown kind are rejected by <c>ck_course_absence_mark</c>.</summary>
    [Theory]
    [InlineData("not_in_classroom", false)]
    [InlineData(null, true)]
    [InlineData("NotInClassroom", true)]
    [InlineData("deleted", true)]
    public async Task AnInvalidMark_IsRejectedByTheCheck(string? mark, bool withInstant)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));

        var failure = await Assert.ThrowsAsync<PostgresException>(() => SetMarkAsync(host, course, mark, withInstant ? T : null, ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal("ck_course_absence_mark", failure.ConstraintName);
    }

    private static Task<int> SetMarkAsync(InstallationTestHost host, long course, string? mark, DateTimeOffset? since, CancellationToken ct) =>
        host.ExecuteAsync(
            "UPDATE course SET absence_mark = @mark, absent_since = @since WHERE id = @id",
            ct,
            ("mark", (object?)mark ?? DBNull.Value),
            ("since", (object?)since ?? DBNull.Value),
            ("id", course));

    // ---- EF Core and the repository (entity model §1 … §6) -----------------------------------------------------------

    private static Course NewCourse(int n) =>
        Course.Import(
            CourseTestData.CourseId(n),
            CourseState.Active,
            new CourseDetails(CourseTestData.CourseName(n), null, null, null, null, null, T, T, null, null, null, null));

    /// <summary>
    /// Entity model §1, §2: each kind is stored as its lower-case code with its instant, the sighting in its column, and
    /// an unmarked course with nulls.
    /// </summary>
    [Fact]
    public async Task TheMarksAndTheSighting_AreStoredAsTheirCodesAndInstants()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        using (var scope = host.CreateScope())
        {
            var courses = scope.ServiceProvider.GetRequiredService<ICourseRepository>();
            var first = NewCourse(1);
            first.RecordListed(T.AddHours(1));
            first.MarkAbsent(CourseAbsenceMark.NotInClassroom, T.AddHours(2));
            var second = NewCourse(2);
            second.MarkAbsent(CourseAbsenceMark.NotVisibleToTechnicalAccount, T.AddHours(3));
            var third = NewCourse(3);
            third.RecordListed(T.AddHours(4));
            courses.Add(first);
            courses.Add(second);
            courses.Add(third);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        var rows = await host.QueryAsync(
            "SELECT google_id, absence_mark, absent_since, last_listed_at FROM course ORDER BY google_id",
            r => (r.GetString(0),
                  r.IsDBNull(1) ? null : r.GetString(1),
                  r.IsDBNull(2) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(2),
                  r.IsDBNull(3) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(3)),
            ct);
        Assert.Equal(
            new (string, string?, DateTimeOffset?, DateTimeOffset?)[]
            {
                (CourseTestData.CourseId(1), "not_in_classroom", T.AddHours(2), T.AddHours(1)),
                (CourseTestData.CourseId(2), "not_visible_to_technical_account", T.AddHours(3), null),
                (CourseTestData.CourseId(3), null, null, T.AddHours(4)),
            },
            rows);
    }

    /// <summary>
    /// Entity model §6: <c>GetAllAsync</c> returns every stored course with its mark read back, tracked — a mark removed
    /// and a sighting recorded on what it returned are saved by the unit of work.
    /// </summary>
    [Fact]
    public async Task EveryCourse_IsLoadedTracked_WithItsMark()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        using (var scope = host.CreateScope())
        {
            var first = NewCourse(1);
            first.MarkAbsent(CourseAbsenceMark.NotVisibleToTechnicalAccount, T);
            scope.ServiceProvider.GetRequiredService<ICourseRepository>().Add(first);
            scope.ServiceProvider.GetRequiredService<ICourseRepository>().Add(NewCourse(2));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        using (var scope = host.CreateScope())
        {
            var all = await scope.ServiceProvider.GetRequiredService<ICourseRepository>().GetAllAsync(ct);
            Assert.Equal(2, all.Count);
            var first = Assert.Single(all, c => c.GoogleId == CourseTestData.CourseId(1));
            Assert.Equal(CourseAbsenceMark.NotVisibleToTechnicalAccount, first.AbsenceMark);
            Assert.Equal(T, first.AbsentSince);
            Assert.True(first.RecordListed(T.AddHours(1)));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        Assert.Equal(
            [(CourseTestData.CourseId(1), true, T.AddHours(1))],
            await host.QueryAsync(
                "SELECT google_id, absence_mark IS NULL AND absent_since IS NULL, last_listed_at FROM course WHERE google_id = @id",
                r => (r.GetString(0), r.GetBoolean(1), r.GetFieldValue<DateTimeOffset>(2)),
                ct,
                ("id", CourseTestData.CourseId(1))));
    }

    /// <summary>Entity model §5, FR-002: a change of the technical account saved through EF Core stores its instant.</summary>
    [Fact]
    public async Task TheChangeInstant_IsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        using (var scope = host.CreateScope())
        {
            var connections = scope.ServiceProvider.GetRequiredService<IWorkspaceConnectionRepository>();
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            connections.Add(WorkspaceConnection.Create(InstallationTestData.Domain, WorkspaceConnectionTestData.TechnicalAccount));
            await work.SaveChangesAsync(ct);
            Assert.Equal([(DateTimeOffset?)null], await ChangeInstantsAsync(host, ct));

            var stored = await connections.GetForUpdateAsync(ct);
            stored!.ChangeTo(InstallationTestData.Domain, WorkspaceConnectionTestData.OtherTechnicalAccount, T);
            await work.SaveChangesAsync(ct);
        }

        Assert.Equal([(DateTimeOffset?)T], await ChangeInstantsAsync(host, ct));
    }

    private static Task<IReadOnlyList<DateTimeOffset?>> ChangeInstantsAsync(InstallationTestHost host, CancellationToken ct) =>
        host.QueryAsync(
            "SELECT technical_account_changed_at FROM workspace_connection",
            r => r.IsDBNull(0) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(0),
            ct);

    /// <summary>Entity model §4, FR-005: the flag set on the state row is stored, and read back.</summary>
    [Fact]
    public async Task TheEmptyListFlag_IsStoredAndReadBack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        using (var scope = host.CreateScope())
        {
            var state = SyncState.BeginFirstRun(Guid.NewGuid(), T);
            state.RecordCourseListRead(true);
            scope.ServiceProvider.GetRequiredService<ISyncStateRepository>().Add(state);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        Assert.True(await host.ScalarAsync<bool>("SELECT last_course_list_empty FROM sync_state", ct));
        using var read = host.CreateScope();
        Assert.True((await read.ServiceProvider.GetRequiredService<ISyncStateRepository>().GetForReadAsync(ct))!.LastCourseListEmpty);
    }
}
