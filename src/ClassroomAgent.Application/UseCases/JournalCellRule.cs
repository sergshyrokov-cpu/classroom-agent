using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The one cell computation of the journal (US-025 spec FR-006) that the report of US-027 reuses (spec FR-004: one
/// computation for every cell), and the date helpers both queries share.
/// </summary>
internal static class JournalCellRule
{
    /// <summary>Spec FR-006: the one pure function of item, submission, view and B.</summary>
    internal static JournalCell Cell(
        JournalItemRecord item,
        JournalSubmissionRecord? submission,
        JournalView view,
        DateTimeOffset b,
        TimeZoneInfo zone)
    {
        if (item.Kind == CourseWorkKind.Material)
        {
            return new JournalCell(JournalCellState.Empty, null, null, null, false, null, null);
        }

        if (submission is null)
        {
            return new JournalCell(JournalCellState.NotAssigned, null, null, null, false, null, null);
        }

        var full = view == JournalView.Full;
        var turnedInOn = full && submission.TurnedInAt is { } turnedInAt ? DateIn(turnedInAt, zone) : (DateOnly?)null;

        if (submission.State == SubmissionState.Unrecognised)
        {
            return new JournalCell(
                JournalCellState.Unrecognised, null, null, submission.RawState, submission.Late, null, turnedInOn);
        }

        var graded = item.Kind == CourseWorkKind.GradedWork;
        if (graded && submission.AssignedGrade is { } grade)
        {
            return new JournalCell(JournalCellState.Grade, grade, item.MaxPoints, null, submission.Late, null, turnedInOn);
        }

        var state = submission.State switch
        {
            SubmissionState.TurnedIn or SubmissionState.StudentEditedAfterTurnIn =>
                graded ? JournalCellState.TurnedInNotGraded : JournalCellState.TurnedIn,
            SubmissionState.Returned => graded ? JournalCellState.ReturnedWithoutGrade : JournalCellState.Returned,
            _ when item.DueAt is null => JournalCellState.NotTurnedInNoDueDate,
            _ when item.DueAt < b => JournalCellState.NotTurnedIn,
            _ => JournalCellState.NotDueYet,
        };
        var draft = full && graded ? submission.DraftGrade : null;
        return new JournalCell(state, null, null, null, submission.Late, draft, turnedInOn);
    }

    /// <summary>An instant as a calendar date of the school's zone (NFR-074).</summary>
    internal static DateOnly DateIn(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>
    /// Spec FR-003: local midnight of <paramref name="date"/> as a UTC instant (offset zero, db-design §2). When
    /// midnight does not exist (a daylight-saving jump), the first valid local minute is used; when it occurs twice,
    /// the earlier instant.
    /// </summary>
    internal static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    /// <summary>
    /// Spec FR-005 condition 1 (I-1): on the roster during part of the period — seen before its end, and either still
    /// on the roster or last seen no earlier than its start.
    /// </summary>
    internal static bool IsOfPeriod(
        DateTimeOffset firstSeenAt, DateTimeOffset lastSeenAt, bool onRoster, DateTimeOffset start, DateTimeOffset end) =>
        firstSeenAt < end && (onRoster || lastSeenAt >= start);

    /// <summary>Spec FR-005, OD-005 (a), I-5: the full name, else the email, else unnamed.</summary>
    internal static (string? Name, JournalNameKind Kind) Label(string? fullName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return (fullName, JournalNameKind.FullName);
        }

        return string.IsNullOrWhiteSpace(email)
            ? (null, JournalNameKind.Unnamed)
            : (email, JournalNameKind.Email);
    }
}
