using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-027 over PostgreSQL (TC-2): items with a stored <c>scheduled_time</c>, created templates inserted as the
/// db-design §2 tables hold them, the rows read back with raw SQL, and the real <see cref="JournalFieldSource"/> and
/// <see cref="ReportTemplateRepository"/> over the host's database. Hosts and sign-in come from
/// <see cref="JournalHostExtensions"/>. Nothing reaches Google (TC-4).
/// </summary>
public static class ReportHostExtensions
{
    /// <summary>The three template tables (db-design §2).</summary>
    public static readonly string[] TemplateTables = ["report_template", "report_template_mark", "report_template_scale_row"];

    /// <summary>Inserts a <c>course_work</c> row with every date the lesson date reads (spec FR-004, FR-020).</summary>
    public static Task<long> InsertLessonAsync(
        this InstallationTestHost host,
        long courseId,
        CancellationToken cancellationToken,
        string googleId,
        DateTimeOffset itemDate,
        DateTimeOffset? scheduledTime = null,
        DateTimeOffset? creationTime = null,
        string resource = CourseWorkTestData.ResourceCodes.CourseWork,
        string title = "Test Lesson",
        DateTimeOffset? dueAt = null,
        decimal? maxPoints = null) =>
        host.ScalarAsync<long>(
            """
            INSERT INTO course_work (course_id, google_id, resource, title, item_date, due_at, max_points,
                                     creation_time, update_time, scheduled_time, created_at, updated_at)
            VALUES (@courseId, @googleId, @resource, @title, @itemDate, @dueAt, @maxPoints,
                    @creationTime, NULL, @scheduledTime, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("courseId", courseId),
            ("googleId", googleId),
            ("resource", resource),
            ("title", title),
            ("itemDate", itemDate),
            ("dueAt", dueAt),
            ("maxPoints", maxPoints),
            ("creationTime", creationTime),
            ("scheduledTime", scheduledTime),
            ("stamp", host.Time.GetUtcNow()))!;

    /// <summary>
    /// Inserts a created template as db-design §2 stores it: the root, nine marks (all <c>program</c> unless given) and
    /// the scale rows. Returns the template id.
    /// </summary>
    public static async Task<long> InsertTemplateAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        string name,
        long authorId,
        string view = "full",
        bool hideMaterials = false,
        int hours = 2,
        IReadOnlyList<(int From, int To, string Label)>? scale = null,
        IReadOnlyDictionary<ReportCellState, (string Kind, string? Text)>? marks = null,
        string lateKind = "program",
        string? lateText = null)
    {
        var stamp = host.Time.GetUtcNow();
        var id = await host.ScalarAsync<long>(
            """
            INSERT INTO report_template (name, normalized_name, view, hide_materials, hours_per_lesson, scale_mode,
                                         late_mark_kind, late_mark_text, author_id, created_at, updated_at)
            VALUES (@name, @normalized, @view, @hide, @hours, @mode, @lateKind, @lateText, @author, @stamp, @stamp)
            RETURNING id
            """,
            cancellationToken,
            ("name", name),
            ("normalized", name.Trim().ToUpperInvariant()),
            ("view", view),
            ("hide", hideMaterials),
            ("hours", hours),
            ("mode", scale is { Count: > 0 } ? "ranges" : "none"),
            ("lateKind", lateKind),
            ("lateText", lateText),
            ("author", authorId),
            ("stamp", stamp));

        foreach (var state in ReportTemplateTestData.States)
        {
            var (kind, text) = marks is not null && marks.TryGetValue(state, out var m) ? m : ("program", null);
            await host.ExecuteAsync(
                """
                INSERT INTO report_template_mark (report_template_id, state, kind, text, created_at, updated_at)
                VALUES (@id, @state, @kind, @text, @stamp, @stamp)
                """,
                cancellationToken,
                ("id", id),
                ("state", StateCode(state)),
                ("kind", kind),
                ("text", text),
                ("stamp", stamp));
        }

        foreach (var (from, to, label) in scale ?? [])
        {
            await host.ExecuteAsync(
                """
                INSERT INTO report_template_scale_row (report_template_id, from_percent, to_percent, label, created_at, updated_at)
                VALUES (@id, @from, @to, @label, @stamp, @stamp)
                """,
                cancellationToken,
                ("id", id),
                ("from", (short)from),
                ("to", (short)to),
                ("label", label),
                ("stamp", stamp));
        }

        return id;
    }

    /// <summary>db-design §2.2: the stored code of a cell state.</summary>
    public static string StateCode(ReportCellState state) => state switch
    {
        ReportCellState.TurnedInNotGraded => "turned_in_not_graded",
        ReportCellState.ReturnedWithoutGrade => "returned_without_grade",
        ReportCellState.TurnedIn => "turned_in",
        ReportCellState.Returned => "returned",
        ReportCellState.NotTurnedIn => "not_turned_in",
        ReportCellState.NotDueYet => "not_due_yet",
        ReportCellState.NotTurnedInNoDueDate => "not_turned_in_no_due_date",
        ReportCellState.NotAssigned => "not_assigned",
        ReportCellState.Unrecognised => "unrecognised",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    /// <summary>The id of the Admin or Dean the host signed in, by email (the author and audit actor of a save).</summary>
    public static Task<long> AccountIdAsync(this InstallationTestHost host, string email, CancellationToken cancellationToken) =>
        host.ScalarAsync<long>("SELECT id FROM app_user WHERE normalized_email = @e", cancellationToken, ("e", email.ToLowerInvariant()))!;

    /// <summary>Every stored template root, ordered by id.</summary>
    public static Task<IReadOnlyList<TemplateRow>> TemplateRowsAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        host.QueryAsync(
            """
            SELECT id, name, normalized_name, view, hide_materials, hours_per_lesson, scale_mode, late_mark_kind,
                   late_mark_text, author_id, created_at, updated_at
            FROM report_template ORDER BY id
            """,
            r => new TemplateRow(
                r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.GetInt32(5),
                r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetInt64(9),
                r.GetFieldValue<DateTimeOffset>(10), r.GetFieldValue<DateTimeOffset>(11)),
            cancellationToken);

    /// <summary>The marks of one template as (state code, kind, text), ordered by state code.</summary>
    public static Task<IReadOnlyList<(string State, string Kind, string? Text)>> MarkRowsAsync(
        this InstallationTestHost host, long templateId, CancellationToken cancellationToken) =>
        host.QueryAsync(
            "SELECT state, kind, text FROM report_template_mark WHERE report_template_id = @id ORDER BY state",
            r => (r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)),
            cancellationToken,
            ("id", templateId));

    /// <summary>The scale rows of one template as (from, to, label), ordered by from.</summary>
    public static Task<IReadOnlyList<(int From, int To, string Label)>> ScaleRowsAsync(
        this InstallationTestHost host, long templateId, CancellationToken cancellationToken) =>
        host.QueryAsync(
            "SELECT from_percent, to_percent, label FROM report_template_scale_row WHERE report_template_id = @id ORDER BY from_percent",
            r => ((int)r.GetInt16(0), (int)r.GetInt16(1), r.GetString(2)),
            cancellationToken,
            ("id", templateId));

    /// <summary>Row count and xmin sum of the template tables: any insert, update or delete changes it.</summary>
    public static async Task<string> TemplateFingerprintAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        var parts = new List<string>();
        foreach (var table in TemplateTables)
        {
            var value = await host.ScalarAsync<string>(
                $"SELECT count(*)::text || ':' || coalesce(sum(xmin::text::bigint), 0)::text FROM {table}",
                cancellationToken);
            parts.Add(table + "=" + value);
        }

        return string.Join(';', parts);
    }

    /// <summary>Runs <paramref name="body"/> with the real <see cref="JournalFieldSource"/> over the host's database.</summary>
    public static async Task WithJournalFieldSourceAsync(
        this InstallationTestHost host, Func<IJournalFieldSource, ClassroomAgentDbContext, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new JournalFieldSource(db), db);
    }

    /// <summary>
    /// Runs <paramref name="body"/> with the real <see cref="ReportTemplateRepository"/> and the host's unit of work
    /// over one scope, as a use case would use them.
    /// </summary>
    public static async Task WithTemplateRepositoryAsync(
        this InstallationTestHost host, Func<IReportTemplateRepository, IUnitOfWork, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new ReportTemplateRepository(db), new UnitOfWork(db));
    }

    /// <summary>One stored <c>report_template</c> row (db-design §2.1).</summary>
    public sealed record TemplateRow(
        long Id,
        string Name,
        string NormalizedName,
        string View,
        bool HideMaterials,
        int HoursPerLesson,
        string ScaleMode,
        string LateMarkKind,
        string? LateMarkText,
        long AuthorId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
