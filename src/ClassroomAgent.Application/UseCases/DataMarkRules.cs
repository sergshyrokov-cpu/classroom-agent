using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.JournalCellRule;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-060: the marks the screens and the export show, computed from stored data only (spec §3, BR-026) — never a Google
/// call, never a write. Dates are calendar dates of the school's zone (spec §4 preamble, VR-004).
/// </summary>
internal static class DataMarkRules
{
    /// <summary>Spec FR-001, VR-004: the stored course mark as a date, or none.</summary>
    internal static CourseAbsence? Absence(CourseAbsenceMark? mark, DateTimeOffset? since, TimeZoneInfo zone) =>
        mark is { } kind && since is { } instant ? new CourseAbsence(kind, DateIn(instant, zone)) : null;

    /// <summary>
    /// Spec FR-003.1, VR-002, VR-003: "not in Classroom" first; then "not visible" or a skip record — "not updated since"
    /// the last complete read, undated when there is none (OD-002 a); otherwise no line.
    /// </summary>
    internal static CourseDataNotice? Notice(JournalCourseRecord course, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(course);
        if (course.AbsenceMark == CourseAbsenceMark.NotInClassroom && course.AbsentSince is { } since)
        {
            return new CourseDataNotice(CourseDataNoticeKind.NotInClassroom, DateIn(since, zone));
        }

        if (course.AbsenceMark == CourseAbsenceMark.NotVisibleToTechnicalAccount || course.Skipped)
        {
            return new CourseDataNotice(
                CourseDataNoticeKind.NotUpdated,
                course.LastCompleteReadAt is { } read ? DateIn(read, zone) : null);
        }

        return null;
    }

    /// <summary>
    /// US-063 spec FR-001, FR-002, VR-001, VR-002: B as given — never a second clock read — and the last complete read,
    /// both in the school's zone; no other field stands in for a missing read (OD-001 a, OD-002 a).
    /// </summary>
    internal static CourseDataStamp Stamp(JournalCourseRecord course, DateTimeOffset b, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(course);
        return new CourseDataStamp(
            TimeZoneInfo.ConvertTime(b, zone),
            course.LastCompleteReadAt is { } read ? TimeZoneInfo.ConvertTime(read, zone) : null);
    }

    /// <summary>Spec FR-006.1, VR-005, OD-003 a: off the roster is "left"; undated when first and last seen are equal.</summary>
    internal static LeftMark? Left(bool onRoster, bool seenOnce, DateTimeOffset lastSeenAt, TimeZoneInfo zone) =>
        onRoster ? null : new LeftMark(seenOnce ? null : DateIn(lastSeenAt, zone));

    /// <summary>Spec FR-004.1, VR-001: strictly greater than a positive maximum; equal is not above.</summary>
    internal static bool IsAboveMaximum(decimal? points, decimal? maxPoints) =>
        points is { } p && maxPoints is { } max && max > 0m && p > max;
}
