using System.Globalization;
using ClassroomAgent.Application.Localization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;
using Microsoft.Extensions.Localization;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// US-060 spec FR-009: the screen texts of the marks, resolved in the current UI culture, with dates in its short date
/// pattern as elsewhere on the page. The marks themselves are computed in Application; this only words them.
/// </summary>
public static class DataMarksTexts
{
    /// <summary>Spec FR-001: "not in Classroom since …" or "not visible to the technical account since …".</summary>
    public static string Absence(IStringLocalizer<SharedResource> texts, CourseAbsence absence)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(absence);
        var key = absence.Mark == CourseAbsenceMark.NotInClassroom
            ? DataMarksTextKeys.CourseNotInClassroomSince
            : DataMarksTextKeys.CourseNotVisibleSince;
        return texts[key, Date(absence.Since)];
    }

    /// <summary>Spec FR-003.1: the course's header line.</summary>
    public static string Notice(IStringLocalizer<SharedResource> texts, CourseDataNotice notice)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(notice);
        return notice switch
        {
            { Kind: CourseDataNoticeKind.NotInClassroom, Since: { } since } =>
                texts[DataMarksTextKeys.HeaderNotInClassroomSince, Date(since)],
            { Since: { } since } => texts[DataMarksTextKeys.HeaderNotUpdatedSince, Date(since)],
            _ => texts[DataMarksTextKeys.HeaderNotUpdated],
        };
    }

    /// <summary>Spec FR-006.1: "left" with the last-seen date, or alone (OD-003 a).</summary>
    public static string Left(IStringLocalizer<SharedResource> texts, LeftMark left)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(left);
        return left.LastSeenOn is { } date ? texts[DataMarksTextKeys.LeftOn, Date(date)] : texts[DataMarksTextKeys.Left];
    }

    /// <summary>US-063 spec FR-001, FR-006: "generated: …" in the school's zone.</summary>
    public static string Generated(IStringLocalizer<SharedResource> texts, CourseDataStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(stamp);
        return texts[DataMarksTextKeys.Generated, DateAndTime(stamp.GeneratedAt)];
    }

    /// <summary>US-063 spec FR-002, FR-006: "data from Google as of: …", or "unknown" when no read is recorded.</summary>
    public static string DataAsOf(IStringLocalizer<SharedResource> texts, CourseDataStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(stamp);
        var value = stamp.DataAsOf is { } read ? DateAndTime(read) : texts[DataMarksTextKeys.DataAsOfUnknown].Value;
        return texts[DataMarksTextKeys.DataAsOf, value];
    }

    /// <summary>US-063 spec FR-006.1, VR-003: short date and hours:minutes, as other date-times on the screens.</summary>
    private static string DateAndTime(DateTimeOffset instant) =>
        instant.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " HH:mm", CultureInfo.CurrentCulture);

    private static string Date(DateOnly date) => date.ToString("d", CultureInfo.CurrentCulture);
}
