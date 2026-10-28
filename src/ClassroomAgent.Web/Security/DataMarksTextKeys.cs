namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the US-060 marks (spec FR-009): course list and card, the journal and report header line,
/// the item, the above-maximum label and notes, the "left" mark. Every key exists in both the Ukrainian and the English
/// file; {0} is a date, or in the notes the points with {1} the maximum.
/// </summary>
public static class DataMarksTextKeys
{
    public const string CourseNotInClassroomSince = "DataMarks.Course.NotInClassroomSince";

    public const string CourseNotVisibleSince = "DataMarks.Course.NotVisibleSince";

    public const string NoTeacher = "DataMarks.Course.NoTeacher";

    public const string HeaderNotInClassroomSince = "DataMarks.Header.NotInClassroomSince";

    public const string HeaderNotUpdatedSince = "DataMarks.Header.NotUpdatedSince";

    public const string HeaderNotUpdated = "DataMarks.Header.NotUpdated";

    public const string ItemNotInClassroom = "DataMarks.Item.NotInClassroom";

    public const string AboveMaximum = "DataMarks.AboveMaximum";

    public const string NotePoints = "DataMarks.Note.Points";

    public const string NoteDraft = "DataMarks.Note.Draft";

    public const string LeftOn = "DataMarks.Left.On";

    public const string Left = "DataMarks.Left";

    /// <summary>US-063 spec FR-006.3: "generated: {0}" — {0} a date and time.</summary>
    public const string Generated = "DataMarks.Generated";

    /// <summary>US-063 spec FR-006.3: "data from Google as of: {0}" — {0} a date and time, or <see cref="DataAsOfUnknown"/>.</summary>
    public const string DataAsOf = "DataMarks.DataAsOf";

    /// <summary>US-063 spec FR-002.3, OD-001 a: the value when no complete read is recorded.</summary>
    public const string DataAsOfUnknown = "DataMarks.DataAsOf.Unknown";
}
