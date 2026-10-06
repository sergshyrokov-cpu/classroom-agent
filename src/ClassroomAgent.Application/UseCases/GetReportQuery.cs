using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The report of one template for one course and period (US-027 spec FR-004, FR-005, FR-011, FR-015). Reads only: no
/// read-only guard, no unit of work, no Google port.
/// </summary>
/// <remarks>
/// The query is validated before any lesson, member, submission or template is read (spec FR-011). The course and
/// template lists are read on every request because the form always shows both drop-downs. The shape rules and the
/// build are <see cref="ReportRequestReader"/> and <see cref="ReportBuilder"/>, which the journal export uses as well, so
/// the file and the screen have one source of content (US-028 spec FR-003, entity model §3.3).
/// </remarks>
public sealed class GetReportQuery(
    IJournalFieldSource fields,
    IReportTemplateRepository templates,
    SchoolTimeZone schoolTimeZone,
    TimeProvider timeProvider)
{
    private readonly ReportBuilder _builder = new(fields, templates, schoolTimeZone, timeProvider);

    public async Task<ReportPageResult> ExecuteAsync(ReportRequest request, CultureInfo uiCulture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(uiCulture);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), schoolTimeZone.Zone).DateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // Spec FR-011, API design §3: shape errors in parameter order template, courseId, from, to, pair, then the
        // name source (US-042 api-design §2.5).
        var selection = ReportRequestReader.Read(request, monthStart, monthStart.AddMonths(1).AddDays(-1), required: false);
        var messages = selection.Messages.ToList();
        var selected = selection.Template;
        var course = selection.CourseId;
        var from = selection.From;
        var to = selection.To;
        var pageSource = selection.PageNameSource;

        // The drop-downs: every stored course and every created template, ordered here with the UI culture's comparer.
        var records = await fields.GetCoursesAsync(cancellationToken);
        var comparer = StringComparer.Create(uiCulture, ignoreCase: false);
        var courses = records
            .OrderBy(c => c.Name, comparer)
            .ThenBy(c => c.Id)
            .Select(c => new CourseOption(c.Id, c.Name, c.Section))
            .ToList();
        var stored = await templates.ListAsync(cancellationToken);
        var options = new List<ReportTemplateOption> { new(ReportTemplateReference.AcademicJournal, true, null) };
        options.AddRange(stored
            .OrderBy(r => r.Name, comparer)
            .ThenBy(r => r.Id)
            .Select(r => new ReportTemplateOption(ReportTemplateReference.Of(r.Id), false, r.Name)));
        var known = course is { } chosen && records.Any(c => c.Id == chosen) ? course : null;

        ReportPageResult Page(
            ReportPageOutcome outcome,
            Report? report = null,
            NameSourceSwitch? nameSwitch = null,
            ExportAction? export = null) =>
            new(
                outcome,
                new ReportPageModel(
                    options,
                    selected,
                    courses,
                    records.Count == 0,
                    known,
                    from,
                    to,
                    messages,
                    ReturnPath(selection.TemplateValid ? selected : null, course, from, to, pageSource),
                    report,
                    nameSwitch,
                    export));

        if (messages.Count > 0)
        {
            return Page(ReportPageOutcome.Invalid);
        }

        if (course is not { } courseId)
        {
            return Page(ReportPageOutcome.Shown);
        }

        // Spec FR-011: existence is decided only for a well-formed request; both keys are reported together.
        var built = await _builder.BuildAsync(selection, records, uiCulture, cancellationToken);
        if (built.Report is not { } report)
        {
            messages.AddRange(built.NotFound);
            return Page(ReportPageOutcome.NotFound);
        }

        var nameSwitch = new NameSourceSwitch(
            new[] { ReportNameSource.Profile, ReportNameSource.Email }
                .Select(s => new NameSourceSwitchOption(s, s == report.NameSource, ReturnPath(selected, courseId, from, to, s)))
                .ToList());

        // US-028 spec FR-002: the export carries the validated values the page was built from, with the effective name
        // source of the switch, so the file never differs from the screen.
        var export = new ExportAction(selected, courseId, report.Header.From, report.Header.To, report.NameSource);
        return Page(ReportPageOutcome.Shown, report, nameSwitch, export);
    }

    /// <summary>
    /// Spec FR-011: the return path of the language switcher, from validated values only; it carries the name source
    /// only when the address carried a valid one (US-042 api-design §2.4). The switch's links use it too (§2.2).
    /// </summary>
    private static string ReturnPath(string? template, long? course, DateOnly? from, DateOnly? to, ReportNameSource? names)
    {
        var parts = new List<string>();
        if (template is not null)
        {
            parts.Add("template=" + Uri.EscapeDataString(template));
        }

        if (course is { } courseId)
        {
            parts.Add("courseId=" + ReportTemplateReference.Of(courseId));
        }

        if (from is { } f)
        {
            parts.Add("from=" + FormatDate(f));
        }

        if (to is { } t)
        {
            parts.Add("to=" + FormatDate(t));
        }

        if (names is { } source)
        {
            parts.Add(NameSourceCode.FieldName + "=" + NameSourceCode.Of(source));
        }

        return parts.Count == 0 ? "/reports" : "/reports?" + string.Join('&', parts);
    }
}
