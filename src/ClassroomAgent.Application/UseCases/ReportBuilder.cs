using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using static ClassroomAgent.Application.UseCases.JournalCellRule;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Builds the report of one template for one course and period (US-027 spec FR-004, FR-005, FR-015; US-042 FR-004,
/// FR-006) — the one source of content of the report page and of every export (US-028 spec FR-003; entity model §3.3).
/// Reads only: no read-only guard, no unit of work, no Google port.
/// </summary>
/// <remarks>
/// Called only with a selection that has no shape message and names a course. Existence is decided first and both
/// not-found keys are reported together (US-027 spec FR-011). Every cell is computed by the journal's own function
/// (<see cref="JournalCellRule"/>) at one instant B and then resolved through the template's settings.
/// </remarks>
internal sealed class ReportBuilder(
    IJournalFieldSource fields,
    IReportTemplateRepository templates,
    SchoolTimeZone schoolTimeZone,
    TimeProvider timeProvider)
{
    private const string ProgramLateKey = "Late";

    public async Task<ReportBuildResult> BuildAsync(
        ReportSelection selection,
        IReadOnlyList<JournalCourseRecord> courses,
        CultureInfo uiCulture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(courses);
        ArgumentNullException.ThrowIfNull(uiCulture);
        if (selection.Messages.Count > 0 || selection.CourseId is null || selection.From is null || selection.To is null)
        {
            throw new ArgumentException("Only a well-formed selection naming a course and a period is built.", nameof(selection));
        }

        var notFound = new List<ReportMessageKey>();
        ReportTemplateSettings settings;
        string? templateName = null;
        if (selection.CreatedTemplateId is { } templateId)
        {
            var createdTemplate = await templates.GetAsync(templateId, forUpdate: false, cancellationToken);
            if (createdTemplate is null)
            {
                notFound.Add(ReportMessageKey.TemplateNotFound);
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

        var courseId = selection.CourseId.Value;
        var courseRecord = courses.SingleOrDefault(c => c.Id == courseId);
        if (courseRecord is null)
        {
            notFound.Add(ReportMessageKey.CourseUnknown);
        }

        if (notFound.Count > 0 || courseRecord is null)
        {
            return new ReportBuildResult(null, notFound);
        }

        var zone = schoolTimeZone.Zone;
        var b = timeProvider.GetUtcNow();
        var comparer = StringComparer.Create(uiCulture, ignoreCase: false);
        var from = selection.From.Value;
        var to = selection.To.Value;

        // US-042 FR-004: the page's valid parameter, else the template's setting; once for every person of the report.
        var nameSource = selection.PageNameSource ?? settings.NameSource;
        var origin = selection.PageNameSource is null ? NameSourceOrigin.Template : NameSourceOrigin.Page;

        var start = StartOfDay(from, zone);
        var end = StartOfDay(to.AddDays(1), zone);

        var lessons = await fields.GetLessonsAsync(courseId, start, end, cancellationToken);
        var members = await fields.GetMembersAsync(courseId, cancellationToken);

        var view = settings.View;
        var teachers = members
            .Where(m => m.Role == ClassroomRole.Teacher && IsOfPeriod(m.FirstSeenAt, m.LastSeenAt, m.OnRoster, start, end))
            .Select(m => (Member: m, Label: ReportPersonNameRule.Label(m.Surname, m.GivenName, m.Email, nameSource)))
            .OrderBy(r => r.Label.Kind == ReportNameKind.Unnamed)
            .ThenBy(r => r.Label.Name, comparer)
            .ThenBy(r => r.Member.ParticipantId)
            .Select(r => new PersonName(r.Label.Name, r.Label.Kind))
            .ToList();
        var header = new ReportHeader(
            templateName is null,
            templateName,
            courseRecord.Name,
            courseRecord.Section,
            from,
            to,
            teachers);

        if (lessons.Count == 0)
        {
            return new ReportBuildResult(
                new Report(header, view, ReportEmptyStateKey.NothingPublished, null, null, nameSource, origin),
                notFound);
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
            .Select(m => (Member: m, Label: ReportPersonNameRule.Label(m.Surname, m.GivenName, m.Email, nameSource)))
            .OrderBy(r => r.Label.Kind == ReportNameKind.Unnamed)
            .ThenBy(r => r.Label.Name, comparer)
            .ThenBy(r => r.Member.ParticipantId)
            .Select(r => new GradingRow(
                new PersonName(r.Label.Name, r.Label.Kind),
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

        return new ReportBuildResult(
            new Report(header, view, null, new GradingPart(gradingColumns, rows), topics, nameSource, origin),
            notFound);
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
}
