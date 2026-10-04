using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.JournalCellRule;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The journal of one course for a period (US-025 spec FR-001 … FR-009). Reads only: no read-only guard, no unit of
/// work, no Google port (spec FR-013) — the constructor is itself the evidence (entity model §4).
/// </summary>
/// <remarks>
/// The query is validated before any journal data is read (spec FR-008). The course list (db-design Q1) is read on
/// every request because the form always shows the drop-down (api-design §2.3), and course existence is decided
/// from it. Q2 … Q4 short-circuit (db-design §2). Every order is applied here with the UI culture's comparer, not by
/// the database (db-design §2), and every cell is computed in memory against one instant B (spec FR-006).
/// </remarks>
public sealed class GetJournalQuery(IJournalSource source, SchoolTimeZone schoolTimeZone, TimeProvider timeProvider)
{
    public async Task<JournalPageResult> ExecuteAsync(JournalRequest request, CultureInfo uiCulture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(uiCulture);

        // Spec FR-006: B is read once per request; FR-007 derives the default period from the same B.
        var zone = schoolTimeZone.Zone;
        var b = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(b, zone).DateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var messages = new List<JournalMessageKey>();
        var course = Single(request.CourseId, out var courseText) switch
        {
            Presence.Absent => (long?)null,
            Presence.One when ParseCourseId(courseText) is { } id => id,
            _ => Refuse<long>(messages, JournalMessageKey.CourseMalformed),
        };
        var from = ReadDate(request.From, monthStart, messages, JournalMessageKey.FromMalformed);
        var to = ReadDate(request.To, monthStart.AddMonths(1).AddDays(-1), messages, JournalMessageKey.ToMalformed);
        var view = Single(request.View, out var viewText) switch
        {
            Presence.Absent => JournalView.Full,
            Presence.One when string.Equals(viewText, "full", StringComparison.OrdinalIgnoreCase) => JournalView.Full,
            Presence.One when string.Equals(viewText, "short", StringComparison.OrdinalIgnoreCase) => JournalView.Short,
            _ => Refuse(messages, JournalMessageKey.ViewUnknown, JournalView.Full),
        };
        if (from is { } f && to is { } t && f > t)
        {
            messages.Add(JournalMessageKey.PeriodInverted);
        }

        // Q1: the drop-down, and the existence of the course (db-design §2).
        var records = await source.GetCoursesAsync(cancellationToken);
        var comparer = StringComparer.Create(uiCulture, ignoreCase: false);
        var courses = records
            .OrderBy(c => c.Name, comparer)
            .ThenBy(c => c.Id)
            .Select(c => new CourseOption(c.Id, c.Name, c.Section))
            .ToList();
        var known = course is { } chosen && records.Any(c => c.Id == chosen) ? course : null;

        JournalPageResult Page(JournalPageOutcome outcome, JournalEmptyStateKey? empty = null, Journal? journal = null) =>
            new(outcome, new JournalPageModel(courses, records.Count == 0, known, from, to, view, messages, empty, journal));

        if (messages.Count > 0)
        {
            return Page(JournalPageOutcome.Invalid);
        }

        if (course is null)
        {
            return Page(JournalPageOutcome.Shown);
        }

        if (known is not { } courseId)
        {
            messages.Add(JournalMessageKey.CourseUnknown);
            return Page(JournalPageOutcome.CourseUnknown);
        }

        // Spec FR-003: whole days of the school's zone as one half-open UTC interval.
        var start = StartOfDay(from!.Value, zone);
        var end = StartOfDay(to!.Value.AddDays(1), zone);

        var items = await source.GetItemsAsync(courseId, start, end, cancellationToken);
        if (items.Count == 0)
        {
            return Page(JournalPageOutcome.Shown, JournalEmptyStateKey.NoColumns);
        }

        var members = await source.GetStudentMembersAsync(courseId, cancellationToken);
        if (members.Count == 0)
        {
            return Page(JournalPageOutcome.Shown, JournalEmptyStateKey.NoRows);
        }

        var submissions = await source.GetSubmissionsAsync(courseId, start, end, cancellationToken);

        var columns = items
            .OrderBy(i => i.ItemDate)
            .ThenBy(i => i.Title, comparer)
            .ThenBy(i => i.Id)
            .ToList();

        // db-design §2.1: a submission to an item Q2 did not return is ignored. OD-008 (a): of several submissions of
        // one item by one student, the latest UpdateTime wins (absent is oldest), then the larger id.
        var itemIds = columns.Select(i => i.Id).ToHashSet();
        var chosenSubmissions = submissions
            .Where(s => itemIds.Contains(s.ItemId))
            .GroupBy(s => (s.ItemId, s.ParticipantId))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.UpdateTime.HasValue)
                    .ThenByDescending(s => s.UpdateTime)
                    .ThenByDescending(s => s.Id)
                    .First());
        var submitters = chosenSubmissions.Keys.Select(k => k.ParticipantId).ToHashSet();

        var rows = members
            .Where(m => IsOfPeriod(m.FirstSeenAt, m.LastSeenAt, m.OnRoster, start, end) || submitters.Contains(m.ParticipantId))
            .Select(m => (Member: m, Label: Label(m.FullName, m.Email)))
            .OrderBy(r => r.Label.Kind == JournalNameKind.Unnamed)
            .ThenBy(r => r.Label.Name, comparer)
            .ThenBy(r => r.Member.ParticipantId)
            .Select(r => new JournalRow(
                r.Label.Name,
                r.Label.Kind,
                columns
                    .Select(item => Cell(
                        item,
                        chosenSubmissions.GetValueOrDefault((item.Id, r.Member.ParticipantId)),
                        view,
                        b,
                        zone))
                    .ToList()))
            .ToList();
        if (rows.Count == 0)
        {
            return Page(JournalPageOutcome.Shown, JournalEmptyStateKey.NoRows);
        }

        var journalColumns = columns
            .Select(i => new JournalColumn(
                i.Title,
                DateIn(i.ItemDate, zone),
                i.Kind,
                i.Kind == CourseWorkKind.GradedWork ? i.MaxPoints : null))
            .ToList();
        return Page(JournalPageOutcome.Shown, journal: new Journal(journalColumns, rows));
    }

    /// <summary>VR-002 through <see cref="JournalQueryRules.ReadDate"/>: a malformed value adds its message.</summary>
    private static DateOnly? ReadDate(
        IReadOnlyList<string?> values,
        DateOnly fallback,
        List<JournalMessageKey> messages,
        JournalMessageKey malformed)
    {
        var date = JournalQueryRules.ReadDate(values, fallback, out var isMalformed);
        if (isMalformed)
        {
            messages.Add(malformed);
        }

        return date;
    }

    private static T? Refuse<T>(List<JournalMessageKey> messages, JournalMessageKey key)
        where T : struct
    {
        messages.Add(key);
        return null;
    }

    private static T Refuse<T>(List<JournalMessageKey> messages, JournalMessageKey key, T fallback)
    {
        messages.Add(key);
        return fallback;
    }
}
