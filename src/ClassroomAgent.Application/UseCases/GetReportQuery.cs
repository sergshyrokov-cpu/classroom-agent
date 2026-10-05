using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using static ClassroomAgent.Application.UseCases.JournalCellRule;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The report of one template for one course and period (US-027 spec FR-004, FR-005, FR-011, FR-015). Reads only: no
/// read-only guard, no unit of work, no Google port.
/// </summary>
/// <remarks>
/// The query is validated before any lesson, member, submission or template is read (spec FR-011). The course and
/// template lists are read on every request because the form always shows both drop-downs. Every cell is computed by
/// the journal's own function (<see cref="JournalCellRule"/>, spec FR-004) at one instant B and then resolved through
/// the template's settings (spec FR-005.4, FR-015).
/// </remarks>
public sealed class GetReportQuery(
    IJournalFieldSource fields,
    IReportTemplateRepository templates,
    SchoolTimeZone schoolTimeZone,
    TimeProvider timeProvider)
{
    private const string ProgramLateKey = "Late";

    public async Task<ReportPageResult> ExecuteAsync(ReportRequest request, CultureInfo uiCulture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(uiCulture);

        var zone = schoolTimeZone.Zone;
        var b = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(b, zone).DateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // Spec FR-011, API design §3: shape errors in parameter order template, courseId, from, to, pair.
        var messages = new List<ReportMessageKey>();
        var selected = ReportTemplateReference.AcademicJournal;
        long? createdId = null;
        var templateValid = true;
        switch (Single(request.Template, out var templateText))
        {
            case Presence.Absent:
                break;
            case Presence.One:
                switch (ReportTemplateReference.Parse(templateText, out var parsedId))
                {
                    case ReportTemplateReference.Kind.BuiltIn:
                        break;
                    case ReportTemplateReference.Kind.Created:
                        createdId = parsedId;
                        selected = ReportTemplateReference.Of(parsedId);
                        break;
                    default:
                        templateValid = false;
                        messages.Add(ReportMessageKey.TemplateMalformed);
                        break;
                }

                break;
            default:
                templateValid = false;
                messages.Add(ReportMessageKey.TemplateMalformed);
                break;
        }

        long? course = null;
        switch (Single(request.CourseId, out var courseText))
        {
            case Presence.Absent:
                break;
            case Presence.One when ParseCourseId(courseText) is { } id:
                course = id;
                break;
            default:
                messages.Add(ReportMessageKey.CourseMalformed);
                break;
        }

        var from = ReadDate(request.From, monthStart, out var fromMalformed);
        if (fromMalformed)
        {
            messages.Add(ReportMessageKey.FromMalformed);
        }

        var to = ReadDate(request.To, monthStart.AddMonths(1).AddDays(-1), out var toMalformed);
        if (toMalformed)
        {
            messages.Add(ReportMessageKey.ToMalformed);
        }

        if (from is { } f && to is { } t && f > t)
        {
            messages.Add(ReportMessageKey.PeriodInverted);
        }

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

        ReportPageResult Page(ReportPageOutcome outcome, Report? report = null) =>
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
                    ReturnPath(templateValid ? selected : null, course, from, to),
                    report));

        if (messages.Count > 0)
        {
            return Page(ReportPageOutcome.Invalid);
        }

        if (course is null)
        {
            return Page(ReportPageOutcome.Shown);
        }

        // Spec FR-011: existence is decided only for a well-formed request; both keys are reported together.
        ReportTemplateSettings settings;
        string? templateName = null;
        if (createdId is { } templateId)
        {
            var createdTemplate = await templates.GetAsync(templateId, forUpdate: false, cancellationToken);
            if (createdTemplate is null)
            {
                messages.Add(ReportMessageKey.TemplateNotFound);
                settings = BuiltInReportTemplates.AcademicJournal;
            }
            else
            {
                settings = createdTemplate.ToSettings();
                templateName = createdTemplate.Name;
            }
        }
        else
        {
            settings = BuiltInReportTemplates.AcademicJournal;
        }

        if (known is null)
        {
            messages.Add(ReportMessageKey.CourseUnknown);
        }

        if (messages.Count > 0 || known is not { } courseId)
        {
            return Page(ReportPageOutcome.NotFound);
        }

        var courseRecord = records.Single(c => c.Id == courseId);
        var start = StartOfDay(from!.Value, zone);
        var end = StartOfDay(to!.Value.AddDays(1), zone);

        var lessons = await fields.GetLessonsAsync(courseId, start, end, cancellationToken);
        var members = await fields.GetMembersAsync(courseId, cancellationToken);

        var view = settings.View;
        var teachers = members
            .Where(m => m.Role == ClassroomRole.Teacher && IsOfPeriod(m.FirstSeenAt, m.LastSeenAt, m.OnRoster, start, end))
            .Select(m => (Member: m, Label: Label(m.FullName, m.Email)))
            .OrderBy(r => r.Label.Kind == JournalNameKind.Unnamed)
            .ThenBy(r => r.Label.Name, comparer)
            .ThenBy(r => r.Member.ParticipantId)
            .Select(r => new PersonName(r.Label.Name, SkeletonKind(r.Label.Kind)))
            .ToList();
        var header = new ReportHeader(
            templateName is null,
            templateName,
            courseRecord.Name,
            courseRecord.Section,
            from.Value,
            to.Value,
            teachers);

        if (lessons.Count == 0)
        {
            return Page(
                ReportPageOutcome.Shown,
                new Report(header, view, ReportEmptyStateKey.NothingPublished, null, null));
        }

        // Spec FR-005.1, FR-005.3: date, then title in the UI collation, then id.
        var ordered = lessons
            .OrderBy(l => l.LessonDate)
            .ThenBy(l => l.Title, comparer)
            .ThenBy(l => l.Id)
            .ToList();
        var columns = settings.HideMaterials
            ? ordered.Where(l => l.Resource != CourseWorkResource.CourseWorkMaterial).ToList()
            : ordered;
        var topics = new LessonTopicsPart(ordered
            .Where(l => l.Resource != CourseWorkResource.CourseWorkMaterial)
            .Select(l => new LessonTopicRow(DateIn(l.LessonDate, zone), l.Title, settings.HoursPerLesson))
            .ToList());
        var gradingColumns = columns
            .Select(l => new GradingColumn(
                DateIn(l.LessonDate, zone), l.Title, l.Resource == CourseWorkResource.CourseWorkMaterial))
            .ToList();

        var submissions = await fields.GetLessonSubmissionsAsync(courseId, start, end, cancellationToken);

        // OD-008 (a) as in the journal: of several submissions of one item by one student the latest UpdateTime wins
        // (absent is oldest), then the larger id. A submission to an item that is not a column is ignored.
        var items = columns.ToDictionary(l => l.Id, ToItem);
        var chosenSubmissions = submissions
            .Where(s => items.ContainsKey(s.ItemId))
            .GroupBy(s => (s.ItemId, s.ParticipantId))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.UpdateTime.HasValue)
                    .ThenByDescending(s => s.UpdateTime)
                    .ThenByDescending(s => s.Id)
                    .First());
        var submitters = chosenSubmissions.Keys.Select(k => k.ParticipantId).ToHashSet();

        var rows = members
            .Where(m => m.Role == ClassroomRole.Student
                && (IsOfPeriod(m.FirstSeenAt, m.LastSeenAt, m.OnRoster, start, end) || submitters.Contains(m.ParticipantId)))
            .Select(m => (Member: m, Label: Label(m.FullName, m.Email)))
            .OrderBy(r => r.Label.Kind == JournalNameKind.Unnamed)
            .ThenBy(r => r.Label.Name, comparer)
            .ThenBy(r => r.Member.ParticipantId)
            .Select(r => new GradingRow(
                new PersonName(r.Label.Name, SkeletonKind(r.Label.Kind)),
                columns
                    .Select(l => ToReportCell(
                        Cell(
                            items[l.Id],
                            chosenSubmissions.GetValueOrDefault((l.Id, r.Member.ParticipantId)),
                            view == ReportView.Full ? JournalView.Full : JournalView.Short,
                            b,
                            zone),
                        items[l.Id],
                        settings))
                    .ToList()))
            .ToList();

        return Page(
            ReportPageOutcome.Shown,
            new Report(header, view, null, new GradingPart(gradingColumns, rows), topics));
    }

    /// <summary>BR-052: a material resource is a material; else no maximum is ungraded; else graded.</summary>
    private static JournalItemRecord ToItem(JournalLessonRecord lesson) => new(
        lesson.Id,
        lesson.Title,
        lesson.LessonDate,
        lesson.DueAt,
        lesson.MaxPoints,
        lesson.Resource == CourseWorkResource.CourseWorkMaterial ? CourseWorkKind.Material
        : lesson.MaxPoints is null ? CourseWorkKind.UngradedWork
        : CourseWorkKind.GradedWork);

    /// <summary>Spec FR-005.4: the journal cell resolved through the template.</summary>
    private static ReportCell ToReportCell(JournalCell cell, JournalItemRecord item, ReportTemplateSettings settings)
    {
        if (cell.State == JournalCellState.Empty)
        {
            return new ReportCell(ReportCellContent.Empty, null, null, null, null, null, null);
        }

        var late = cell.Late ? LateMark(settings.LateMark) : null;
        var draft = cell.DraftPoints is { } draftPoints ? Grade(draftPoints, item.MaxPoints, settings) : null;

        if (cell.State == JournalCellState.Grade)
        {
            return new ReportCell(
                ReportCellContent.Grade,
                Grade(cell.Points ?? 0m, cell.MaxPoints, settings),
                null,
                null,
                late,
                draft,
                cell.TurnedInOn);
        }

        var state = cell.State switch
        {
            JournalCellState.NotAssigned => ReportCellState.NotAssigned,
            JournalCellState.Unrecognised => ReportCellState.Unrecognised,
            JournalCellState.TurnedInNotGraded => ReportCellState.TurnedInNotGraded,
            JournalCellState.ReturnedWithoutGrade => ReportCellState.ReturnedWithoutGrade,
            JournalCellState.TurnedIn => ReportCellState.TurnedIn,
            JournalCellState.Returned => ReportCellState.Returned,
            JournalCellState.NotTurnedIn => ReportCellState.NotTurnedIn,
            JournalCellState.NotDueYet => ReportCellState.NotDueYet,
            _ => ReportCellState.NotTurnedInNoDueDate,
        };
        var mark = settings.Marks[state];
        if (mark.Kind == ReportMarkKind.Empty)
        {
            return new ReportCell(ReportCellContent.Empty, null, null, null, late, draft, cell.TurnedInOn);
        }

        if (mark.Kind == ReportMarkKind.Program && state == ReportCellState.Unrecognised)
        {
            return new ReportCell(ReportCellContent.RawState, null, null, cell.RawState, late, draft, cell.TurnedInOn);
        }

        var reportMark = mark.Kind == ReportMarkKind.Program
            ? new ReportCellMark(ReportCellMarkKind.Program, state.ToString(), null)
            : new ReportCellMark(ReportCellMarkKind.Own, null, mark.Text);
        return new ReportCell(ReportCellContent.Mark, null, reportMark, null, late, draft, cell.TurnedInOn);
    }

    private static ReportCellMark? LateMark(ReportLateMark mark) => mark.Kind switch
    {
        ReportLateMarkKind.Program => new ReportCellMark(ReportCellMarkKind.Program, ProgramLateKey, null),
        ReportLateMarkKind.Own => new ReportCellMark(ReportCellMarkKind.Own, null, mark.Text),
        _ => null,
    };

    /// <summary>
    /// Spec FR-015: with ranges the percent is points over maximum, rounded to a whole number with 0.5 up and clamped
    /// to 0 … 100, and the cell shows the label of the row containing it; with no conversion, or a maximum that is not
    /// positive, the raw points out of the maximum.
    /// </summary>
    private static ReportGrade Grade(decimal points, decimal? max, ReportTemplateSettings settings)
    {
        if (settings.ScaleMode == ReportScaleMode.Ranges && max is > 0m)
        {
            var percent = (int)Math.Clamp(
                Math.Round(points / max.Value * 100m, 0, MidpointRounding.AwayFromZero), 0m, 100m);
            var row = settings.ScaleRows.FirstOrDefault(r => r.FromPercent <= percent && percent <= r.ToPercent);
            if (row is not null)
            {
                return new ReportGrade(ReportGradeKind.ScaleLabel, row.Label, null, null);
            }
        }

        return new ReportGrade(ReportGradeKind.RawPoints, null, points, max);
    }

    /// <summary>
    /// US-042 skeleton (OD-001): carries the US-027 label kind into the report's own <see cref="ReportNameKind"/> so the
    /// changed DTO compiles with today's behaviour. IMPLEMENTATION replaces it with the spec FR-003 rule.
    /// </summary>
    private static ReportNameKind SkeletonKind(JournalNameKind kind) => kind switch
    {
        JournalNameKind.FullName => ReportNameKind.Profile,
        JournalNameKind.Email => ReportNameKind.EmailLocalPart,
        _ => ReportNameKind.Unnamed,
    };

    /// <summary>Spec FR-011: the return path of the language switcher, from validated values only.</summary>
    private static string ReturnPath(string? template, long? course, DateOnly? from, DateOnly? to)
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

        return parts.Count == 0 ? "/reports" : "/reports?" + string.Join('&', parts);
    }
}
