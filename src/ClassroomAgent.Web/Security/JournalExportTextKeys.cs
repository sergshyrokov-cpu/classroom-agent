namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the journal export (US-028 api-design §5, spec FR-013): the program text of the file, the
/// page's export controls and the new error messages. Every key exists in both the Ukrainian and the English file.
/// </summary>
public static class JournalExportTextKeys
{
    /// <summary>Name of the grading sheet (at most 31 characters in every language).</summary>
    public const string SheetGrading = "Export.Sheet.Grading";

    /// <summary>Name of the lesson-topics sheet (at most 31 characters in every language).</summary>
    public const string SheetLessonTopics = "Export.Sheet.LessonTopics";

    public const string HeaderTemplate = "Export.Header.Template";

    public const string HeaderCourse = "Export.Header.Course";

    /// <summary>The word used in the file name when the course has no usable name.</summary>
    public const string CourseFallback = "Export.Course.Fallback";

    /// <summary>The label of the export button on the report page.</summary>
    public const string Action = "Report.Export.Action";

    public const string OrientationCaption = "Report.Export.Orientation";

    public const string OrientationPortrait = "Report.Export.Orientation.Portrait";

    public const string OrientationLandscape = "Report.Export.Orientation.Landscape";

    public const string OrientationInvalid = "Export.Validation.OrientationInvalid";

    public const string RequestMalformed = "Export.Validation.RequestMalformed";

    public const string SignInRequired = "Api.Error.SignInRequired";

    public const string PasswordChangeRequired = "Api.Error.PasswordChangeRequired";
}
