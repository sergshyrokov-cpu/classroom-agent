using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-044 db-design §3 and §7, against the migrated database (TC-2): a valid row of each Meet export action and a
/// journal row are accepted, and each new or rewritten check constraint rejects its violating row by direct insert,
/// named in the <see cref="PostgresException"/>.
/// </summary>
public sealed class MeetExportAuditSchemaTests(PostgreSqlFixture database)
{
    private const string ColumnsAbsent = "ck_audit_event_export_columns_absent";
    private const string JournalShape = "ck_audit_event_export_shape";
    private const string ReportsShape = "ck_audit_event_meet_reports_export_shape";
    private const string MeetingShape = "ck_audit_event_meet_meeting_export_shape";
    private const string MeetCodeAbsent = "ck_audit_event_meet_code_absent";

    private const string InsertExportRow =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, export_period_from, export_period_to,
                                 export_template_id, export_template_built_in, export_rows, export_meetings,
                                 export_meeting_id, export_format, meet_code, created_at, updated_at)
        VALUES (now(), 'app_user', 1, 'dean', @action, @targetType, @targetId, @outcome, @refusalCategory,
                'request-id', @periodFrom::date, @periodTo::date, @templateId::bigint, @builtIn::boolean,
                @rows::integer, @meetings::integer, @meetingId::bigint, @format, @meetCode, now(), now())
        """;

    /// <summary>db-design §3.2: a course-workbook row with the period, both counts and no meeting id is valid.</summary>
    [Fact]
    public async Task AValidMeetReportsRow_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, Row.Reports(), ct);

        Assert.Equal(
            1L,
            await host.CountAsync(
                "audit_event",
                ct,
                "action = 'meet_reports_exported' AND export_period_from = date '2026-10-01' "
                + "AND export_period_to = date '2026-10-31' AND export_rows = 3 AND export_meetings = 2 "
                + "AND export_meeting_id IS NULL AND export_format = 'xlsx' AND meet_code IS NULL"));
    }

    /// <summary>db-design §3.2: a meeting-workbook row with the meeting id, rows and no period is valid.</summary>
    [Fact]
    public async Task AValidMeetMeetingRow_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, Row.Meeting(), ct);

        Assert.Equal(
            1L,
            await host.CountAsync(
                "audit_event",
                ct,
                "action = 'meet_meeting_exported' AND export_meeting_id = 7 AND export_rows = 3 "
                + "AND export_period_from IS NULL AND export_meetings IS NULL AND meet_code IS NULL"));
    }

    /// <summary>db-design §3.3: the rewritten constraints still accept a US-028 journal row.</summary>
    [Fact]
    public async Task AValidJournalRow_IsStillAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await InsertAsync(host, Row.Journal(), ct);

        Assert.Equal(1L, await host.CountAsync("audit_event", ct, "action = 'journal_exported'"));
    }

    /// <summary>db-design §3.3: a course-workbook row names the meeting rows it listed.</summary>
    [Fact]
    public async Task AMeetReportsRowWithoutTheMeetingCount_IsRejected() =>
        await AssertRejectedAsync(Row.Reports() with { Meetings = null }, ReportsShape);

    /// <summary>db-design §3.3: a course-workbook row uses no template.</summary>
    [Fact]
    public async Task AMeetReportsRowWithATemplateId_IsRejected() =>
        await AssertRejectedAsync(Row.Reports() with { TemplateId = 5 }, ReportsShape);

    /// <summary>db-design §3.3: a course-workbook row names no single meeting.</summary>
    [Fact]
    public async Task AMeetReportsRowWithAMeetingId_IsRejected() =>
        await AssertRejectedAsync(Row.Reports() with { MeetingId = 7 }, ReportsShape);

    /// <summary>db-design §3.3: a count of student rows is never negative.</summary>
    [Fact]
    public async Task AMeetReportsRowWithANegativeRowCount_IsRejected() =>
        await AssertRejectedAsync(Row.Reports() with { Rows = -1 }, ReportsShape);

    /// <summary>db-design §3.3: a meeting-workbook row has no period.</summary>
    [Fact]
    public async Task AMeetMeetingRowWithAPeriod_IsRejected() =>
        await AssertRejectedAsync(
            Row.Meeting() with { PeriodFrom = new DateOnly(2026, 10, 1), PeriodTo = new DateOnly(2026, 10, 31) },
            MeetingShape);

    /// <summary>db-design §3.3: a meeting-workbook row has no meeting count.</summary>
    [Fact]
    public async Task AMeetMeetingRowWithAMeetingCount_IsRejected() =>
        await AssertRejectedAsync(Row.Meeting() with { Meetings = 2 }, MeetingShape);

    /// <summary>db-design §3.3: a meeting-workbook row names its meeting.</summary>
    [Fact]
    public async Task AMeetMeetingRowWithoutAMeetingId_IsRejected() =>
        await AssertRejectedAsync(Row.Meeting() with { MeetingId = null }, MeetingShape);

    /// <summary>db-design §3.3: a meeting id is a generated positive id.</summary>
    [Fact]
    public async Task AMeetMeetingRowWithMeetingIdZero_IsRejected() =>
        await AssertRejectedAsync(Row.Meeting() with { MeetingId = 0 }, MeetingShape);

    /// <summary>db-design §3.3: the journal row has neither Meet column — no meeting id.</summary>
    [Fact]
    public async Task AJournalRowWithAMeetingId_IsRejected() =>
        await AssertRejectedAsync(Row.Journal() with { MeetingId = 7 }, JournalShape);

    /// <summary>db-design §3.3: the journal row has neither Meet column — no meeting count.</summary>
    [Fact]
    public async Task AJournalRowWithAMeetingCount_IsRejected() =>
        await AssertRejectedAsync(Row.Journal() with { Meetings = 2 }, JournalShape);

    /// <summary>db-design §3.3: no export column on a row of any other action — a sign-in row with a meeting count.</summary>
    [Fact]
    public async Task AnotherActionCarryingAMeetingCount_IsRejected() =>
        await AssertRejectedAsync(
            new Row("dean_sign_in", null, null, "succeeded", null, null, null, null, null, null, 2, null, null, null),
            ColumnsAbsent);

    /// <summary>db-design §3.3: the audit row never carries the meeting code, on either Meet export action.</summary>
    [Fact]
    public async Task AMeetReportsRowWithAMeetCode_IsRejected() =>
        await AssertRejectedAsync(Row.Reports() with { MeetCode = "aaa-0001-xyz" }, MeetCodeAbsent);

    /// <summary>db-design §3.3: the audit row never carries the meeting code, on either Meet export action.</summary>
    [Fact]
    public async Task AMeetMeetingRowWithAMeetCode_IsRejected() =>
        await AssertRejectedAsync(Row.Meeting() with { MeetCode = "aaa-0001-xyz" }, MeetCodeAbsent);

    /// <summary>db-design §3.3: only a successful export is audited.</summary>
    [Fact]
    public async Task ARefusedMeetReportsRow_IsRejected() =>
        await AssertRejectedAsync(
            Row.Reports() with { Outcome = "refused", RefusalCategory = "read_only_mode" },
            ReportsShape);

    /// <summary>db-design §3.3: only a successful export is audited.</summary>
    [Fact]
    public async Task ARefusedMeetMeetingRow_IsRejected() =>
        await AssertRejectedAsync(
            Row.Meeting() with { Outcome = "refused", RefusalCategory = "read_only_mode" },
            MeetingShape);

    private async Task AssertRejectedAsync(Row row, string constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(host, row, ct));

        Assert.Equal(constraint, failure.ConstraintName);
    }

    private static Task<int> InsertAsync(InstallationTestHost host, Row row, CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            InsertExportRow,
            cancellationToken,
            ("action", row.Action),
            ("targetType", row.TargetType),
            ("targetId", row.TargetId),
            ("outcome", row.Outcome),
            ("refusalCategory", row.RefusalCategory),
            ("periodFrom", row.PeriodFrom),
            ("periodTo", row.PeriodTo),
            ("templateId", row.TemplateId),
            ("builtIn", row.BuiltIn),
            ("rows", row.Rows),
            ("meetings", row.Meetings),
            ("meetingId", row.MeetingId),
            ("format", row.Format),
            ("meetCode", row.MeetCode));

    /// <summary>One <c>audit_event</c> insert; each factory is the valid row of its action, a case changes one member.</summary>
    private sealed record Row(
        string Action,
        string? TargetType,
        long? TargetId,
        string Outcome,
        string? RefusalCategory,
        DateOnly? PeriodFrom,
        DateOnly? PeriodTo,
        long? TemplateId,
        bool? BuiltIn,
        int? Rows,
        int? Meetings,
        long? MeetingId,
        string? Format,
        string? MeetCode)
    {
        private static readonly DateOnly From = new(2026, 10, 1);

        private static readonly DateOnly To = new(2026, 10, 31);

        public static Row Reports() =>
            new("meet_reports_exported", "course", 42, "succeeded", null, From, To, null, null, 3, 2, null, "xlsx", null);

        public static Row Meeting() =>
            new("meet_meeting_exported", "course", 42, "succeeded", null, null, null, null, null, 3, null, 7, "xlsx", null);

        public static Row Journal() =>
            new("journal_exported", "course", 42, "succeeded", null, From, To, null, true, 3, null, null, "xlsx", null);
    }
}
