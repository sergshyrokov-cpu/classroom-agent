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

    private static string Date(DateOnly date) => date.ToString("d", CultureInfo.CurrentCulture);
}
