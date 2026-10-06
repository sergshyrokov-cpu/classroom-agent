using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-028 db-design §3 and §8, against the migrated database (TC-2): the six export columns exist as designed and are
/// nullable; a valid export row (built-in or created template) is accepted; and each of the four new check
/// constraints rejects its violating row by direct insert, named in the <see cref="PostgresException"/>.
/// </summary>
public sealed class JournalExportAuditSchemaTests(PostgreSqlFixture database)
{
    private const string Columns = "ck_audit_event_export_columns";
    private const string ColumnsAbsent = "ck_audit_event_export_columns_absent";
    private const string Shape = "ck_audit_event_export_shape";
    private const string TemplateId = "ck_audit_event_export_template_id";

    private const string InsertExportRow =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, export_period_from, export_period_to,
                                 export_template_id, export_template_built_in, export_rows, export_format,
                                 created_at, updated_at)
        VALUES (now(), @actorType, @actorId, @actorRole, @action, @targetType, @targetId, @outcome, @refusalCategory,
                'request-id', @periodFrom::date, @periodTo::date, @templateId::bigint, @builtIn::boolean,
                @rows::integer, @format, now(), now())
        """;

    /// <summary>db-design §3.2: type, length and nullability of the six new columns.</summary>
    [Fact]
    public async Task TheSixExportColumns_MatchTheDesign()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT column_name, data_type, is_nullable, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'audit_event' AND column_name LIKE 'export\_%'
            ORDER BY column_name
            """,
            r => $"{r.GetString(0)} {r.GetString(1)} {r.GetString(2)} {(r.IsDBNull(3) ? "-" : r.GetInt32(3).ToString())}",
            ct);

        Assert.Equal(
            new[]
            {
                "export_format character varying YES 8",
                "export_period_from date YES -",
                "export_period_to date YES -",
                "export_rows integer YES -",
                "export_template_built_in boolean YES -",
                "export_template_id bigint YES -",
            },
            columns);
    }

    /// <summary>db-design §3.4: the four new constraints exist under the designed names.</summary>
    [Fact]
    public async Task TheFourExportConstraints_AreNamedAsTheDesignFixesThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var checks = await InstallationSchemaQueries.CheckConstraintsAsync(host, "audit_event", ct);

        Assert.Equal(
            new[] { Columns, ColumnsAbsent, Shape, TemplateId },
            checks.Where(c => c.StartsWith("ck_audit_event_export", StringComparison.Ordinal)));
    }

    /// <summary>db-design §3.2: a built-in-template export of an empty report is a valid row.</summary>
    [Fact]
    public async Task AValidBuiltInTemplateExportRow_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct);

        Assert.Equal(1L, await host.CountAsync("audit_event", ct, "action = 'journal_exported'"));
        Assert.Equal(
            1L,
            await host.CountAsync(
                "audit_event",
                ct,
                "export_period_from = date '2026-09-01' AND export_period_to = date '2026-09-30' "
                + "AND export_template_id IS NULL AND export_template_built_in = true AND export_rows = 0 "
                + "AND export_format = 'xlsx' AND target_type = 'course' AND target_id = 42"));
    }

    /// <summary>db-design §3.2: a created template is recorded by id, with the built-in marker false.</summary>
    [Fact]
    public async Task AValidCreatedTemplateExportRow_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, ct, templateId: 7, builtIn: false);

        Assert.Equal(
            1L,
            await host.CountAsync(
                "audit_event",
                ct,
                "action = 'journal_exported' AND export_template_id = 7 AND export_template_built_in = false"));
    }

    /// <summary>db-design §3.4: every required detail is on an export row — a missing row count is rejected.</summary>
    [Fact]
    public async Task AnExportRowMissingTheRowCount_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, ct, rows: null));

        Assert.Equal(Columns, failure.ConstraintName);
    }

    /// <summary>db-design §3.4: none on any other row — a sign-in row carrying only a template id is rejected.</summary>
    [Fact]
    public async Task AnotherActionCarryingAnExportColumn_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            action: "dean_sign_in",
            targetType: null,
            targetId: null,
            periodFrom: null,
            periodTo: null,
            templateId: 7,
            builtIn: null,
            rows: null,
            format: null));

        Assert.Equal(ColumnsAbsent, failure.ConstraintName);
    }

    /// <summary>db-design §3.4: the period runs forward.</summary>
    [Fact]
    public async Task APeriodThatEndsBeforeItStarts_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            periodFrom: new DateOnly(2026, 9, 30),
            periodTo: new DateOnly(2026, 9, 1)));

        Assert.Equal(Shape, failure.ConstraintName);
    }

    /// <summary>db-design §3.4: a count of student rows is never negative.</summary>
    [Fact]
    public async Task ANegativeRowCount_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, ct, rows: -1));

        Assert.Equal(Shape, failure.ConstraintName);
    }

    /// <summary>db-design §3.4, D-3: exactly one template form — the built-in marker never comes with an id.</summary>
    [Fact]
    public async Task ABuiltInTemplateWithAnId_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            templateId: 7,
            builtIn: true));

        Assert.Equal(Shape, failure.ConstraintName);
    }

    /// <summary>db-design §3.4, D-3: a created template is always named by its id.</summary>
    [Fact]
    public async Task ACreatedTemplateWithoutAnId_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            templateId: null,
            builtIn: false));

        Assert.Equal(Shape, failure.ConstraintName);
    }

    /// <summary>db-design §3.4: the export row names a course, nothing else.</summary>
    [Fact]
    public async Task AnExportRowNamingAnotherTargetType_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            targetType: "report_template"));

        Assert.Equal(Shape, failure.ConstraintName);
    }

    /// <summary>db-design §3.4, spec FR-005: only a successful export is audited.</summary>
    [Fact]
    public async Task ARefusedExportRow_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            outcome: "refused",
            refusalCategory: "read_only_mode"));

        Assert.Equal(Shape, failure.ConstraintName);
    }

    /// <summary>db-design §3.4, D-4: the format is a known code — <c>xlsx</c> only for now.</summary>
    [Fact]
    public async Task AnUnknownExportFormat_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, ct, format: "docx"));

        Assert.Equal(Shape, failure.ConstraintName);
    }

    /// <summary>db-design §3.4: a template id is a generated positive id.</summary>
    [Fact]
    public async Task ANonPositiveTemplateId_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            host,
            ct,
            templateId: -1,
            builtIn: false));

        Assert.Equal(TemplateId, failure.ConstraintName);
    }

    private static Task<int> InsertAsync(
        InstallationTestHost host,
        CancellationToken cancellationToken,
        string actorType = "app_user",
        long? actorId = 1,
        string? actorRole = "dean",
        string action = "journal_exported",
        string? targetType = "course",
        long? targetId = 42,
        string outcome = "succeeded",
        string? refusalCategory = null,
        DateOnly? periodFrom = null,
        DateOnly? periodTo = null,
        long? templateId = null,
        bool? builtIn = true,
        int? rows = 0,
        string? format = "xlsx") =>
        host.ExecuteAsync(
            InsertExportRow,
            cancellationToken,
            ("actorType", actorType),
            ("actorId", actorId),
            ("actorRole", actorRole),
            ("action", action),
            ("targetType", targetType),
            ("targetId", targetId),
            ("outcome", outcome),
            ("refusalCategory", refusalCategory),
            ("periodFrom", action == "journal_exported" ? periodFrom ?? new DateOnly(2026, 9, 1) : periodFrom),
            ("periodTo", action == "journal_exported" ? periodTo ?? new DateOnly(2026, 9, 30) : periodTo),
            ("templateId", templateId),
            ("builtIn", builtIn),
            ("rows", rows),
            ("format", format));
}
