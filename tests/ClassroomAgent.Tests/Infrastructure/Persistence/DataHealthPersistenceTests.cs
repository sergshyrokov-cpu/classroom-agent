using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-062 db-design §2 … §7 against real PostgreSQL (TC-2): the migration <c>AddDataHealth</c> and its backfill
/// (FR-008.2, OD-002 a), <c>sync_state.course_list_empty_since</c> and its CHECK, the confirmation audit row's
/// <c>confirmed_course_ids</c> and its CHECKs, the two partial indexes, the row-locked read of the confirmation (§4) and
/// the read port <see cref="IDataHealthSource"/> (§5). All values are synthetic (TC-4).
/// </summary>
public sealed class DataHealthPersistenceTests(PostgreSqlFixture database)
{
    /// <summary>The last migration before this Story — the schema every existing installation has.</summary>
    private const string BeforeThisStory = "20261027110753_AddCourseListMarks";

    private const string MigrationSuffix = "_AddDataHealth";

    private const string EmptySinceCheck = "ck_sync_state_course_list_empty_since";

    private const string ShapeCheck = "ck_audit_event_course_deletion_shape";

    private const string IdsAbsentCheck = "ck_audit_event_confirmed_course_ids_absent";

    private static readonly DateTimeOffset T = new(2026, 9, 10, 21, 30, 0, TimeSpan.Zero);

    private static ClassroomAgentDbContext Context(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ClassroomAgentDbContext>();
        ClassroomAgentDbContextOptions.Configure(builder, connectionString);
        builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        return new ClassroomAgentDbContext(builder.Options);
    }

    private Task<InstallationTestHost> StartWritableAsync(CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, connection: SeededConnection.None);

    private static Task<int> InsertStateAsync(InstallationTestHost host, bool empty, DateTimeOffset? since, CancellationToken ct) =>
        host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_successful_run_at, last_course_list_empty, course_list_empty_since, created_at, updated_at)
            VALUES (true, 'completed', @runId, @started, @finished, 0, @finished, @empty, @since, @started, @started)
            """,
            ct,
            ("runId", Guid.NewGuid()),
            ("started", T),
            ("finished", T.AddMinutes(5)),
            ("empty", empty),
            ("since", since));

    private static Task<int> InsertAuditAsync(
        InstallationTestHost host,
        CancellationToken ct,
        string action = "courses_confirmed_deleted",
        string outcome = "succeeded",
        string? refusalCategory = null,
        string actorRole = "admin",
        long[]? ids = null,
        string? targetType = null,
        long? targetId = null) =>
        host.ExecuteAsync(
            """
            INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                     outcome, refusal_category, request_id, confirmed_course_ids, created_at, updated_at)
            VALUES (now(), 'app_user', 1, @role, @action, @targetType, @targetId, @outcome, @category, 'request-id',
                    @ids, now(), now())
            """,
            ct,
            ("role", actorRole),
            ("action", action),
            ("targetType", targetType),
            ("targetId", targetId),
            ("outcome", outcome),
            ("category", refusalCategory),
            ("ids", ids));

    private static async Task<string> CheckFailureAsync(Func<Task> insert)
    {
        var failure = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        return failure.ConstraintName!;
    }

    // ---- Migration (db-design §7) ---------------------------------------------------------------------------------

    /// <summary>Db-design §7, PC-2: the Story ships exactly one migration under the designed name, after the last one.</summary>
    [Fact]
    public async Task TheMigration_ExistsUnderTheDesignedName_AfterTheLastOne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await using var db = Context(host.ConnectionString);

        var migrations = db.Database.GetMigrations().ToList();

        var ours = Assert.Single(migrations, m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
        Assert.Equal(ours, migrations[^1]);
        Assert.True(string.CompareOrdinal(ours, BeforeThisStory) > 0);
    }

    /// <summary>
    /// AC-016, FR-008.2, OD-002 a: a row stored before this Story with the flag set gets the start of its last run as the
    /// date; a row without the flag stays without a date.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheMigration_DatesAFlaggedRowWithItsStart_AndLeavesAnUnflaggedOne(bool flagged)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct, migrate: false);
        await using (var db = Context(host.ConnectionString))
        {
            Assert.Single(db.Database.GetMigrations(), m => m.EndsWith(MigrationSuffix, StringComparison.Ordinal));
            await db.GetService<IMigrator>().MigrateAsync(BeforeThisStory, ct);
        }

        await host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_successful_run_at, last_course_list_empty, created_at, updated_at)
            VALUES (true, 'completed', @runId, @started, @finished, 0, @finished, @empty, @started, @started)
            """,
            ct,
            ("runId", Guid.NewGuid()),
            ("started", T),
            ("finished", T.AddMinutes(5)),
            ("empty", flagged));

        await using (var db = Context(host.ConnectionString))
        {
            await db.Database.MigrateAsync(ct);
        }

        var since = Assert.Single(await host.QueryAsync(
            "SELECT course_list_empty_since FROM sync_state",
            r => r.IsDBNull(0) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(0),
            ct));
        Assert.Equal(flagged ? T : null, since);
    }

    // ---- sync_state.course_list_empty_since (db-design §2) ----------------------------------------------------------

    /// <summary>Db-design §2: the column is <c>timestamptz</c>, nullable.</summary>
    [Fact]
    public async Task TheDateColumn_IsANullableTimestamptz()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var shape = await host.QueryAsync(
            """
            SELECT data_type || '|' || is_nullable FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'sync_state' AND column_name = 'course_list_empty_since'
            """,
            r => r.GetString(0),
            ct);

        Assert.Equal(["timestamp with time zone|YES"], shape);
    }

    /// <summary>FR-008.1, db-design §2: the flag and the date agree — set together or absent together.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AFlagAndDateThatDisagree_AreRejectedByTheCheck(bool empty, bool withDate)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(EmptySinceCheck, await CheckFailureAsync(() => InsertStateAsync(host, empty, withDate ? T : null, ct)));
    }

    /// <summary>FR-008.1: a flag with its date, and neither, are accepted.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AFlagAndDateThatAgree_AreAccepted(bool empty)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(1, await InsertStateAsync(host, empty, empty ? T : null, ct));
    }

    /// <summary>Entity model §1.1: the date set on the entity is stored and read back.</summary>
    [Fact]
    public async Task TheDate_IsStoredAndReadBack()
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

        // Npgsql returns timestamptz as a UTC DateTime (the MeetSessionRepositoryTests precedent).
        Assert.Equal(
            T, new DateTimeOffset(await host.ScalarAsync<DateTime>("SELECT course_list_empty_since FROM sync_state", ct), TimeSpan.Zero));
        using var read = host.CreateScope();
        var stored = await read.ServiceProvider.GetRequiredService<ISyncStateRepository>().GetForReadAsync(ct);
        Assert.Equal(T, stored!.CourseListEmptySince);
    }

    // ---- audit_event (db-design §3) ---------------------------------------------------------------------------------

    /// <summary>Db-design §3.1: the column is a nullable <c>bigint[]</c>.</summary>
    [Fact]
    public async Task TheIdsColumn_IsANullableBigintArray()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var shape = await host.QueryAsync(
            """
            SELECT data_type || '|' || udt_name || '|' || is_nullable FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'audit_event' AND column_name = 'confirmed_course_ids'
            """,
            r => r.GetString(0),
            ct);

        Assert.Equal(["ARRAY|_int8|YES"], shape);
    }

    /// <summary>Db-design §3.2: a succeeded row with 1 … 1000 ids, and the refused read-only row without ids, are accepted.</summary>
    [Fact]
    public async Task TheDesignedRows_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(1, await InsertAuditAsync(host, ct, ids: [4, 9]));
        Assert.Equal(1, await InsertAuditAsync(host, ct, ids: Enumerable.Range(1, 1000).Select(i => (long)i).ToArray()));
        Assert.Equal(1, await InsertAuditAsync(host, ct, outcome: "refused", refusalCategory: "read_only_mode"));
    }

    /// <summary>
    /// Db-design §3.2: the shape CHECK refuses a succeeded row without ids, with no id or more than 1000, a refused row
    /// with ids or another category, a Dean actor and a target.
    /// </summary>
    [Theory]
    [InlineData("succeeded", null, "admin", 0, false)]
    [InlineData("succeeded", null, "admin", -1, false)]
    [InlineData("succeeded", null, "admin", 1001, false)]
    [InlineData("refused", "read_only_mode", "admin", 2, false)]
    [InlineData("refused", "wrong_password", "admin", -1, false)]
    [InlineData("succeeded", null, "dean", 2, false)]
    [InlineData("succeeded", null, "admin", 2, true)]
    public async Task AConfirmationRowOutsideTheShape_IsRejected(
        string outcome, string? category, string role, int idCount, bool withTarget)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        long[]? ids = idCount < 0 ? null : Enumerable.Range(1, idCount).Select(i => (long)i).ToArray();

        var constraint = await CheckFailureAsync(() => InsertAuditAsync(
            host, ct, outcome: outcome, refusalCategory: category, actorRole: role, ids: ids,
            targetType: withTarget ? "app_user" : null, targetId: withTarget ? 5 : null));

        Assert.StartsWith("ck_audit_event", constraint, StringComparison.Ordinal);
    }

    /// <summary>Db-design §3.2: no other action may carry ids.</summary>
    [Fact]
    public async Task AnotherActionWithIds_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.Equal(
            IdsAbsentCheck,
            await CheckFailureAsync(() => InsertAuditAsync(host, ct, action: "admin_sign_in", ids: [3])));
    }

    /// <summary>Db-design §3.2: both new constraints exist under their names, and the action CHECK knows the new code.</summary>
    [Fact]
    public async Task TheNewConstraints_Exist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var checks = await InstallationSchemaQueries.CheckConstraintsAsync(host, "audit_event", ct);

        Assert.Contains(ShapeCheck, checks);
        Assert.Contains(IdsAbsentCheck, checks);
        Assert.Contains(EmptySinceCheck, await InstallationSchemaQueries.CheckConstraintsAsync(host, "sync_state", ct));
    }

    /// <summary>Entity model §1.4, db-design §3.2: the factory's row is saved through EF Core and stored with its ids.</summary>
    [Fact]
    public async Task TheFactorysRow_IsStoredWithItsIds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var admin = await host.ScalarAsync<long>(
            """
            INSERT INTO app_user (email, normalized_email, role, sign_in_method, security_stamp, concurrency_stamp,
                                  access_failed_count, ui_language, is_disabled, created_at, updated_at)
            VALUES ('admin@school-one.example.test', 'admin@school-one.example.test', 'admin', 'google', 's', 'c', 0, 'uk',
                    false, now(), now())
            RETURNING id
            """,
            ct);
        using (var scope = host.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IAuditEventRepository>()
                .Add(AuditEvent.CoursesConfirmedDeleted(admin, [12L, 5L], T, "request-1"));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        var row = Assert.Single(await host.AuditRowsAsync(ct), r => r.Action == "courses_confirmed_deleted");
        Assert.Equal("succeeded", row.Outcome);
        Assert.Equal(admin, row.ActorId);
        Assert.Equal("admin", row.ActorRole);
        Assert.Null(row.TargetType);
        Assert.Equal([new long[] { 5, 12 }], await host.ConfirmedIdsAsync(ct));
    }

    // ---- Indexes (db-design §5.2) -----------------------------------------------------------------------------------

    /// <summary>Db-design §5.2: the two partial indexes exist on their columns with their predicates.</summary>
    [Theory]
    [InlineData("submission", "ix_submission_graded", "(course_work_id) INCLUDE (assigned_grade, draft_grade)", "assigned_grade IS NOT NULL")]
    [InlineData("course_work", "ix_course_work_not_in_classroom", "(course_id)", "not_in_classroom_since IS NOT NULL")]
    public async Task ThePartialIndex_Exists(string table, string index, string columns, string predicate)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = Assert.Single(await host.QueryAsync(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = @table AND indexname = @index",
            r => r.GetString(0),
            ct,
            ("table", table),
            ("index", index)));

        Assert.Contains(columns, definition, StringComparison.Ordinal);
        Assert.Contains(predicate, definition, StringComparison.Ordinal);
    }

    // ---- The row-locked read (db-design §4) -------------------------------------------------------------------------

    /// <summary>Entity model §2.1: the read returns the known ids only, as tracked entities a save writes back.</summary>
    [Fact]
    public async Task TheLockedRead_ReturnsKnownCoursesOnly_Tracked()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var first = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));
        var second = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(2));
        await host.MarkCourseAsync(first, "not_visible_to_technical_account", T, ct);

        using (var scope = host.CreateScope())
        {
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var courses = scope.ServiceProvider.GetRequiredService<ICourseRepository>();
            await work.ExecuteInTransactionAsync(
                async token =>
                {
                    var found = await courses.GetForAbsenceConfirmationAsync([first, 999_999], token);
                    var course = Assert.Single(found);
                    Assert.Equal(first, course.Id);
                    course.ConfirmDeletedInClassroom(T.AddDays(1));
                    await work.SaveChangesAsync(token);
                },
                ct);
        }

        Assert.Equal(("not_in_classroom", (DateTimeOffset?)T.AddDays(1)), await host.MarkOfAsync(first, ct));
        Assert.Equal(((string?)null, (DateTimeOffset?)null), await host.MarkOfAsync(second, ct));
    }

    /// <summary>
    /// Db-design §4: the read locks the selected rows <c>FOR UPDATE</c> until the transaction ends — a second session
    /// asking for the same row without waiting is refused (55P03), and gets it once the transaction is over.
    /// </summary>
    [Fact]
    public async Task TheLockedRead_HoldsARowLockUntilTheTransactionEnds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var course = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));

        async Task<string?> TryLockElsewhereAsync()
        {
            await using var other = new NpgsqlConnection(host.ConnectionString);
            await other.OpenAsync(ct);
            await using var tx = await other.BeginTransactionAsync(ct);
            await using var command = new NpgsqlCommand("SELECT id FROM course WHERE id = @id FOR UPDATE NOWAIT", other, tx);
            command.Parameters.AddWithValue("id", course);
            try
            {
                await command.ExecuteScalarAsync(ct);
                return null;
            }
            catch (PostgresException failure)
            {
                return failure.SqlState;
            }
        }

        string? whileHeld = null;
        using (var scope = host.CreateScope())
        {
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var courses = scope.ServiceProvider.GetRequiredService<ICourseRepository>();
            await work.ExecuteInTransactionAsync(
                async token =>
                {
                    Assert.Single(await courses.GetForAbsenceConfirmationAsync([course], token));
                    whileHeld = await TryLockElsewhereAsync();
                },
                ct);
        }

        Assert.Equal(PostgresErrorCodes.LockNotAvailable, whileHeld);
        Assert.Null(await TryLockElsewhereAsync());
    }

    // ---- IDataHealthSource (db-design §5) ---------------------------------------------------------------------------

    /// <summary>
    /// Db-design §5.1, spec §1.1, I-10: the fact query's above-maximum boolean is <c>DataMarkRules.IsAboveMaximum</c> —
    /// both present, maximum above zero, points strictly above it — for the grade and for the draft grade.
    /// </summary>
    [Theory]
    [InlineData("10", "10.5", null, true)]
    [InlineData("10", null, "11", true)]
    [InlineData("10", "10", null, false)]
    [InlineData("10", "9", "10", false)]
    [InlineData("0", "3", null, false)]
    [InlineData(null, "3", "4", false)]
    [InlineData("10", null, null, false)]
    public async Task TheAboveMaximumFact_FollowsTheDomainRule(string? maximum, string? assigned, string? draft, bool expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await host.ReadyToSeedAsync(ct);
        var course = await host.CourseAsync(1, "Test Facts Course", ct);
        await host.GradedAsync(course, 1, "Test Item", Parse(maximum), "Studentone", Parse(assigned), Parse(draft), ct);
        await host.MarkCourseAsync(course, "not_in_classroom", T, ct);

        var fact = Assert.Single(await FactsAsync(host, ct));

        Assert.Equal(course, fact.CourseId);
        Assert.Equal(expected, fact.HasGradeAboveMaximum);
        Assert.False(fact.HasItemNotInClassroom);
    }

    /// <summary>
    /// Db-design §5.1: a row per course with a mark, an above-maximum grade, an item gone or a skip record matched by
    /// Google id — and none for a course with no such fact. The row carries the stored columns.
    /// </summary>
    [Fact]
    public async Task TheFactQuery_ReturnsExactlyTheCoursesWithAFact()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await host.ReadyToSeedAsync(ct);
        var plain = await host.CourseAsync(1, "Test Plain", ct);
        await host.GradedAsync(plain, 1, "Test Plain Item", 10m, "Plainstudent", 10m, null, ct);
        var marked = await host.CourseAsync(2, "Test Marked", ct, state: CourseTestData.States.Archived, lastCompleteReadAt: T);
        await host.MarkCourseAsync(marked, "not_visible_to_technical_account", T.AddDays(1), ct);
        var over = await host.CourseAsync(3, "Test Over", ct);
        await host.GradedAsync(over, 3, "Test Over Item", 10m, "Overstudent", 12m, null, ct);
        var gone = await host.CourseAsync(4, "Test Gone", ct);
        await host.ItemNotInClassroomAsync(gone, 4, "Test Gone Item", T, ct);
        var skipped = await host.CourseAsync(5, "Test Skipped", ct);
        await host.SkipAsync(CourseTestData.CourseId(5), "Test Skipped", DataHealthHostExtensions.ValueOutOfRangeReason, T, ct);
        await host.SkipAsync(CourseTestData.CourseId(99), "Test Never Loaded", DataHealthHostExtensions.ValueOutOfRangeReason, T, ct);

        var facts = (await FactsAsync(host, ct)).ToDictionary(f => f.CourseId);

        Assert.Equal(new[] { marked, over, gone, skipped }.Order(), facts.Keys.Order());
        var m = facts[marked];
        Assert.Equal(CourseTestData.CourseId(2), m.GoogleId);
        Assert.Equal("Test Marked", m.Name);
        Assert.Equal(CourseState.Archived, m.State);
        Assert.Equal(CourseAbsenceMark.NotVisibleToTechnicalAccount, m.AbsenceMark);
        Assert.Equal(T.AddDays(1), m.AbsentSince);
        Assert.Equal(T, m.LastCompleteReadAt);
        Assert.True(facts[over].HasGradeAboveMaximum);
        Assert.True(facts[gone].HasItemNotInClassroom);
        Assert.False(facts[skipped].HasGradeAboveMaximum);
    }

    /// <summary>
    /// Db-design §5.3, FR-004.3: one row per grade above maximum — the assigned and the draft grade of one submission are
    /// two rows — with the journal's student label, ordered by item title then student; other courses not included.
    /// </summary>
    [Fact]
    public async Task TheAboveMaximumDetails_AreOneRowPerGrade_ByItemThenStudent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await host.ReadyToSeedAsync(ct);
        var course = await host.CourseAsync(1, "Test Details", ct);
        var other = await host.CourseAsync(2, "Test Other", ct);
        var b = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, googleId: CourseWorkTestData.ItemId(1), title: "B Item", maxPoints: 10m);
        var a = await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, googleId: CourseWorkTestData.ItemId(2), title: "A Item", maxPoints: 5m);
        await host.SubmissionAsync(course, b, 1, "Zeta", 11m, 12m, ct);
        await host.SubmissionAsync(course, a, 2, "Zeta2", 6m, null, ct);
        await host.SubmissionAsync(course, a, 3, "Alpha", null, 7m, ct);
        await host.SubmissionAsync(course, a, 4, "Equal", 5m, null, ct);
        await host.GradedAsync(other, 5, "Other Item", 1m, "Otherstudent", 2m, null, ct);

        IReadOnlyList<AboveMaximumGradeRow> rows;
        using (var scope = host.CreateScope())
        {
            rows = await scope.ServiceProvider.GetRequiredService<IDataHealthSource>().GetAboveMaximumGradesAsync([course], ct);
        }

        Assert.Equal(
            new[]
            {
                ("A Item", "Alpha Test", 7m, 5m, true),
                ("A Item", "Zeta2 Test", 6m, 5m, false),
                ("B Item", "Zeta Test", 11m, 10m, false),
                ("B Item", "Zeta Test", 12m, 10m, true),
            },
            rows.Select(r => (r.ItemTitle, r.StudentName, r.Points, r.Maximum, r.IsDraft)));
        Assert.All(rows, r => Assert.Equal(course, r.CourseId));
    }

    /// <summary>Db-design §5.3: the items marked "not in Classroom" of the given courses, with their mark's instant.</summary>
    [Fact]
    public async Task TheItemsGone_AreListedWithTheirInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await host.ReadyToSeedAsync(ct);
        var course = await host.CourseAsync(1, "Test Items", ct);
        await host.ItemNotInClassroomAsync(course, 1, "Test Gone Item", T, ct);
        await CourseWorkRows.InsertCourseWorkAsync(host, course, ct, googleId: CourseWorkTestData.ItemId(2), title: "Test Present Item");

        IReadOnlyList<ItemNotInClassroomRow> rows;
        using (var scope = host.CreateScope())
        {
            rows = await scope.ServiceProvider.GetRequiredService<IDataHealthSource>().GetItemsNotInClassroomAsync([course], ct);
        }

        var row = Assert.Single(rows);
        Assert.Equal((course, "Test Gone Item", T), (row.CourseId, row.Title, row.Since));
    }

    private static decimal? Parse(string? value) =>
        value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<IReadOnlyList<CourseHealthFact>> FactsAsync(InstallationTestHost host, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDataHealthSource>().GetCourseFactsAsync(ct);
    }
}
