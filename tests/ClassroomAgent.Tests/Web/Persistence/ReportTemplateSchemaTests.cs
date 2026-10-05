using System.Globalization;
using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-027 db-design §2, §3, §4, §7 (AC-007, AC-012): the migration creates the three template tables with the
/// designed columns, unique indexes, foreign keys and check constraints, adds <c>course_work.scheduled_time</c>, and
/// widens the audit checks — against real PostgreSQL (PC-2, TC-2); the InMemory provider enforces none of this.
/// </summary>
public sealed class ReportTemplateSchemaTests(PostgreSqlFixture database)
{
    private const string InsertRoot =
        """
        INSERT INTO report_template (name, normalized_name, view, hide_materials, hours_per_lesson, scale_mode,
                                     late_mark_kind, late_mark_text, author_id, created_at, updated_at)
        VALUES (@name, @normalized, @view, @hide, @hours, @mode, @lateKind, @lateText, 1, @stamp, @stamp)
        RETURNING id
        """;

    /// <summary>The same root once US-042 adds the non-null <c>name_source</c> (db-design §3), which has no default.</summary>
    private const string InsertRootWithNameSource =
        """
        INSERT INTO report_template (name, normalized_name, view, hide_materials, hours_per_lesson, scale_mode,
                                     late_mark_kind, late_mark_text, author_id, created_at, updated_at, name_source)
        VALUES (@name, @normalized, @view, @hide, @hours, @mode, @lateKind, @lateText, 1, @stamp, @stamp, 'profile')
        RETURNING id
        """;

    private const string InsertMark =
        """
        INSERT INTO report_template_mark (report_template_id, state, kind, text, created_at, updated_at)
        VALUES (@template, @state, @kind, @text, @stamp, @stamp)
        """;

    private const string InsertScaleRow =
        """
        INSERT INTO report_template_scale_row (report_template_id, from_percent, to_percent, label, created_at, updated_at)
        VALUES (@template, @from, @to, @label, @stamp, @stamp)
        """;

    private const string InsertAudit =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, created_at, updated_at)
        VALUES (@stamp, @actorType, @actorId, @role, @action, @targetType, @targetId,
                @outcome, @category, 'r-1', @stamp, @stamp)
        """;

    private static readonly string[] ExpectedColumns =
    [
        "report_template|id|bigint||NO",
        "report_template|name|character varying|100|NO",
        "report_template|normalized_name|character varying|100|NO",
        "report_template|view|character varying|8|NO",
        "report_template|hide_materials|boolean||NO",
        "report_template|hours_per_lesson|integer||NO",
        "report_template|scale_mode|character varying|8|NO",
        "report_template|late_mark_kind|character varying|8|NO",
        "report_template|late_mark_text|character varying|30|YES",
        "report_template|author_id|bigint||NO",
        "report_template|created_at|timestamp with time zone||NO",
        "report_template|updated_at|timestamp with time zone||NO",
        "report_template|name_source|character varying|8|NO", // US-042 db-design §3
        "report_template_mark|id|bigint||NO",
        "report_template_mark|report_template_id|bigint||NO",
        "report_template_mark|state|character varying|32|NO",
        "report_template_mark|kind|character varying|8|NO",
        "report_template_mark|text|character varying|30|YES",
        "report_template_mark|created_at|timestamp with time zone||NO",
        "report_template_mark|updated_at|timestamp with time zone||NO",
        "report_template_scale_row|id|bigint||NO",
        "report_template_scale_row|report_template_id|bigint||NO",
        "report_template_scale_row|from_percent|smallint||NO",
        "report_template_scale_row|to_percent|smallint||NO",
        "report_template_scale_row|label|character varying|10|NO",
        "report_template_scale_row|created_at|timestamp with time zone||NO",
        "report_template_scale_row|updated_at|timestamp with time zone||NO",
    ];

    /// <summary>db-design §2.1 … §2.3: tables, columns, types, lengths, nullability, unique indexes and cascading foreign keys.</summary>
    [Fact]
    public async Task TheTemplateTables_HaveTheDesignedColumns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT table_name || '|' || column_name || '|' || data_type || '|'
                   || coalesce(character_maximum_length::text, '') || '|' || is_nullable
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name IN ('report_template', 'report_template_mark', 'report_template_scale_row')
            """,
            r => r.GetString(0),
            ct);
        var uniqueIndexes = await host.QueryAsync(
            """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = current_schema() AND indexdef LIKE 'CREATE UNIQUE INDEX%'
              AND tablename IN ('report_template', 'report_template_mark', 'report_template_scale_row')
            """,
            r => r.GetString(0),
            ct);
        var foreignKeys = await host.QueryAsync(
            """
            SELECT conname || '|' || confrelid::regclass::text || '|' || confdeltype::text
            FROM pg_constraint
            WHERE contype = 'f' AND conrelid IN ('report_template'::regclass, 'report_template_mark'::regclass,
                                                  'report_template_scale_row'::regclass)
            """,
            r => r.GetString(0),
            ct);
        var scheduled = await host.QueryAsync(
            """
            SELECT data_type || '|' || is_nullable FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'course_work' AND column_name = 'scheduled_time'
            """,
            r => r.GetString(0),
            ct);

        Assert.Equal(ExpectedColumns.Order(StringComparer.Ordinal), columns.Order(StringComparer.Ordinal));
        Assert.Contains("uq_report_template_normalized_name", uniqueIndexes);
        Assert.Contains("uq_report_template_mark_template_state", uniqueIndexes);
        Assert.Contains("uq_report_template_scale_row_template_from", uniqueIndexes);
        Assert.Equal(
            [
                "fk_report_template_mark_report_template|report_template|c",
                "fk_report_template_scale_row_report_template|report_template|c",
            ],
            foreignKeys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["timestamp with time zone|YES"], scheduled);
    }

    /// <summary>db-design §3, AC-012: the mirror column exists, <c>timestamptz</c> and nullable (a row without it is storable).</summary>
    [Fact]
    public async Task CourseWork_HasANullableScheduledTime()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var when = new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);

        var scheduled = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(1), itemDate: JournalTestData.Period.Early, scheduledTime: when);
        var unscheduled = await host.InsertLessonAsync(
            course, ct, CourseWorkTestData.ItemId(2), itemDate: JournalTestData.Period.Early);

        var stored = await host.QueryAsync(
            "SELECT id, scheduled_time FROM course_work ORDER BY id",
            r => (Id: r.GetInt64(0), At: r.IsDBNull(1) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(1)),
            ct);
        Assert.Equal([(scheduled, (DateTimeOffset?)when), (unscheduled, null)], stored);
    }

    /// <summary>db-design §2.1 … §2.3: each check refuses its invalid row, by name (every other value is valid).</summary>
    [Theory]
    [InlineData("report_template", "view=wide", "ck_report_template_view")]
    [InlineData("report_template", "hours_per_lesson=0", "ck_report_template_hours_per_lesson")]
    [InlineData("report_template", "hours_per_lesson=11", "ck_report_template_hours_per_lesson")]
    [InlineData("report_template", "scale_mode=x", "ck_report_template_scale_mode")]
    [InlineData("report_template", "late_mark_kind=bold", "ck_report_template_late_mark_kind")]
    [InlineData("report_template", "late_mark_kind=own", "ck_report_template_late_mark_text")]
    [InlineData("report_template", "late_mark_text=oops", "ck_report_template_late_mark_text")]
    [InlineData("report_template_mark", "kind=bold", "ck_report_template_mark_kind")]
    [InlineData("report_template_mark", "state=unknown", "ck_report_template_mark_state")]
    [InlineData("report_template_mark", "kind=own", "ck_report_template_mark_text")]
    [InlineData("report_template_mark", "text=oops", "ck_report_template_mark_text")]
    [InlineData("report_template_scale_row", "from_percent=50;to_percent=40", "ck_report_template_scale_row_bounds")]
    [InlineData("report_template_scale_row", "to_percent=101", "ck_report_template_scale_row_bounds")]
    [InlineData("report_template_scale_row", "from_percent=-1", "ck_report_template_scale_row_bounds")]
    public async Task TheCheckConstraints_RefuseInvalidRows(string table, string overrides, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var values = Parse(overrides);

        var insert = async () =>
        {
            if (table == "report_template")
            {
                await InsertRootAsync(host, ct, values);
                return;
            }

            var template = await InsertRootAsync(host, ct, new Dictionary<string, object?>());
            await (table == "report_template_mark"
                ? InsertMarkAsync(host, template, ct, values)
                : InsertScaleRowAsync(host, template, ct, values));
        };

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    /// <summary>db-design §2.1 … §2.3: the unique indexes and the foreign keys refuse the rows they exist for.</summary>
    [Fact]
    public async Task TheUniqueIndexesAndForeignKeys_RefuseDuplicatesAndOrphans()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var template = await InsertRootAsync(host, ct, new Dictionary<string, object?>());
        await InsertMarkAsync(host, template, ct, new Dictionary<string, object?>());
        await InsertScaleRowAsync(host, template, ct, new Dictionary<string, object?>());

        var sameName = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRootAsync(host, ct, new Dictionary<string, object?>()));
        var sameState = await Assert.ThrowsAsync<PostgresException>(
            () => InsertMarkAsync(host, template, ct, new Dictionary<string, object?>()));
        var sameFrom = await Assert.ThrowsAsync<PostgresException>(
            () => InsertScaleRowAsync(host, template, ct, new Dictionary<string, object?>()));
        var orphanMark = await Assert.ThrowsAsync<PostgresException>(
            () => InsertMarkAsync(host, template + 1000, ct, new Dictionary<string, object?>()));
        var orphanRow = await Assert.ThrowsAsync<PostgresException>(
            () => InsertScaleRowAsync(host, template + 1000, ct, new Dictionary<string, object?>()));

        Assert.Equal(("23505", "uq_report_template_normalized_name"), (sameName.SqlState, sameName.ConstraintName));
        Assert.Equal(("23505", "uq_report_template_mark_template_state"), (sameState.SqlState, sameState.ConstraintName));
        Assert.Equal(("23505", "uq_report_template_scale_row_template_from"), (sameFrom.SqlState, sameFrom.ConstraintName));
        Assert.Equal(("23503", "fk_report_template_mark_report_template"), (orphanMark.SqlState, orphanMark.ConstraintName));
        Assert.Equal(("23503", "fk_report_template_scale_row_report_template"), (orphanRow.SqlState, orphanRow.ConstraintName));
    }

    /// <summary>db-design §4, AC-007: the rows the use cases write are accepted; misshapen ones violate the shape check.</summary>
    [Fact]
    public async Task TheAuditShapeCheck_AcceptsTemplateRows_AndRefusesMisshapenOnes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        const string shape = "ck_audit_event_report_template_shape";

        await AuditAsync(host, ct, "report_template_created", "succeeded", "report_template", 5L, null);
        await AuditAsync(host, ct, "report_template_changed", "succeeded", "report_template", 5L, null);
        await AuditAsync(host, ct, "report_template_deleted", "succeeded", "report_template", 5L, null);
        await AuditAsync(host, ct, "report_template_created", "refused", "report_template", null, "read_only_mode");
        await AuditAsync(host, ct, "report_template_changed", "refused", "report_template", null, "read_only_mode");
        await AuditAsync(host, ct, "report_template_changed", "refused", "report_template", 7L, "read_only_mode");
        await AuditAsync(host, ct, "report_template_deleted", "refused", "report_template", 7L, "read_only_mode");

        await RefusedAsync(host, ct, shape, "report_template_changed", "succeeded", "report_template", null, null);
        await RefusedAsync(host, ct, shape, "report_template_created", "succeeded", "app_user", 5L, null);
        await RefusedAsync(host, ct, shape, "report_template_created", "refused", "report_template", null, "connection_not_usable");
        await RefusedAsync(host, ct, shape, "report_template_created", "refused", "report_template", 7L, "read_only_mode");
        await RefusedAsync(host, ct, shape, "report_template_created", "succeeded", "report_template", 5L, null, actorType: "system");

        Assert.Equal(7, (await host.AuditRowsAsync(ct)).Count);
    }

    /// <summary>db-design §7: the migration ships with the entities — the model has nothing a migration lacks.</summary>
    [Fact]
    public async Task TheModelHasNoPendingChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.WaitForPurgeRunsAsync(1, ct); // the start-up purge deletes participants without a membership

        Assert.False(host.HasPendingModelChanges());
    }

    private static Dictionary<string, object?> Parse(string overrides)
    {
        var values = new Dictionary<string, object?>();
        foreach (var pair in overrides.Split(';'))
        {
            var parts = pair.Split('=', 2);
            values[parts[0]] = parts[0] switch
            {
                "hours_per_lesson" => int.Parse(parts[1], CultureInfo.InvariantCulture),
                "from_percent" or "to_percent" => short.Parse(parts[1], CultureInfo.InvariantCulture),
                _ => parts[1],
            };
        }

        return values;
    }

    private static object? Value(IReadOnlyDictionary<string, object?> values, string column, object? fallback) =>
        values.TryGetValue(column, out var value) ? value : fallback;

    private static async Task<long> InsertRootAsync(InstallationTestHost host, CancellationToken ct, IReadOnlyDictionary<string, object?> values) =>
        await host.ScalarAsync<long>(
            await host.ColumnExistsAsync("report_template", "name_source", ct) ? InsertRootWithNameSource : InsertRoot,
            ct,
            ("name", "Test Name"),
            ("normalized", "TEST NAME"),
            ("view", Value(values, "view", "full")),
            ("hide", false),
            ("hours", Value(values, "hours_per_lesson", 2)),
            ("mode", Value(values, "scale_mode", "none")),
            ("lateKind", Value(values, "late_mark_kind", "program")),
            ("lateText", Value(values, "late_mark_text", null)),
            ("stamp", host.Time.GetUtcNow()))!;

    private static Task<int> InsertMarkAsync(
        InstallationTestHost host, long template, CancellationToken ct, IReadOnlyDictionary<string, object?> values) =>
        host.ExecuteAsync(
            InsertMark,
            ct,
            ("template", template),
            ("state", Value(values, "state", "turned_in")),
            ("kind", Value(values, "kind", "program")),
            ("text", Value(values, "text", null)),
            ("stamp", host.Time.GetUtcNow()));

    private static Task<int> InsertScaleRowAsync(
        InstallationTestHost host, long template, CancellationToken ct, IReadOnlyDictionary<string, object?> values) =>
        host.ExecuteAsync(
            InsertScaleRow,
            ct,
            ("template", template),
            ("from", Value(values, "from_percent", (short)0)),
            ("to", Value(values, "to_percent", (short)100)),
            ("label", "x"),
            ("stamp", host.Time.GetUtcNow()));

    private static Task<int> AuditAsync(
        InstallationTestHost host,
        CancellationToken ct,
        string action,
        string outcome,
        string? targetType,
        long? targetId,
        string? category,
        string actorType = "app_user") =>
        host.ExecuteAsync(
            InsertAudit,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("actorType", actorType),
            ("actorId", actorType == "app_user" ? 1L : null),
            ("role", actorType == "app_user" ? "dean" : null),
            ("action", action),
            ("targetType", targetType),
            ("targetId", targetId),
            ("outcome", outcome),
            ("category", category));

    private static async Task RefusedAsync(
        InstallationTestHost host,
        CancellationToken ct,
        string constraint,
        string action,
        string outcome,
        string? targetType,
        long? targetId,
        string? category,
        string actorType = "app_user")
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => AuditAsync(host, ct, action, outcome, targetType, targetId, category, actorType));

        Assert.Equal("23514", error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }
}
