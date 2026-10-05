using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the report templates and the on-screen report (US-027 spec FR-017; api-design §2.9). The
/// openapi enums map to one key family each; program text of report cells reuses the US-025 <c>Journal.*</c> keys.
/// Every key exists in both the Ukrainian and the English file.
/// </summary>
public static class ReportTemplateTextKeys
{
    public const string Section = "ReportTemplate.Section";

    public const string NavigationEntry = "ReportTemplate.NavigationEntry";

    public const string ListTitle = "ReportTemplate.List.Title";

    public const string NameHeader = "ReportTemplate.List.Name";

    public const string AuthorHeader = "ReportTemplate.List.Author";

    public const string ChangedHeader = "ReportTemplate.List.ChangedAt";

    public const string ActionsHeader = "ReportTemplate.List.Actions";

    public const string BuiltInName = "ReportTemplate.BuiltIn.AcademicJournal";

    public const string BuiltInMarker = "ReportTemplate.BuiltIn.Marker";

    public const string AccountDeleted = "ReportTemplate.AccountDeleted";

    /// <summary>F-1 of the test generation report: " (копія)" in Ukrainian, " (copy)" in English; the leading space is part of it.</summary>
    public const string CopySuffix = "ReportTemplate.Copy.Suffix";

    public const string ActionReport = "ReportTemplate.Action.Report";

    public const string ActionCopy = "ReportTemplate.Action.Copy";

    public const string ActionChange = "ReportTemplate.Action.Change";

    public const string ActionDelete = "ReportTemplate.Action.Delete";

    public const string ActionNew = "ReportTemplate.Action.New";

    public const string FormTitleNew = "ReportTemplate.Form.TitleNew";

    public const string FormTitleCopy = "ReportTemplate.Form.TitleCopy";

    public const string FormTitleChange = "ReportTemplate.Form.TitleChange";

    public const string FormName = "ReportTemplate.Form.Name";

    public const string FormView = "ReportTemplate.Form.View";

    public const string FormHideMaterials = "ReportTemplate.Form.HideMaterials";

    public const string FormYes = "ReportTemplate.Form.Yes";

    public const string FormNo = "ReportTemplate.Form.No";

    public const string FormHours = "ReportTemplate.Form.Hours";

    public const string FormScale = "ReportTemplate.Form.Scale";

    public const string FormScaleNone = "ReportTemplate.Form.ScaleNone";

    public const string FormScaleRanges = "ReportTemplate.Form.ScaleRanges";

    public const string FormScaleFrom = "ReportTemplate.Form.ScaleFrom";

    public const string FormScaleTo = "ReportTemplate.Form.ScaleTo";

    public const string FormScaleLabel = "ReportTemplate.Form.ScaleLabel";

    public const string FormScaleAddRow = "ReportTemplate.Form.ScaleAddRow";

    public const string FormScaleRemoveRow = "ReportTemplate.Form.ScaleRemoveRow";

    public const string FormScalePreset = "ReportTemplate.Form.ScalePreset";

    public const string FormMarks = "ReportTemplate.Form.Marks";

    public const string FormMarkProgram = "ReportTemplate.Form.MarkProgram";

    public const string FormMarkOwn = "ReportTemplate.Form.MarkOwn";

    public const string FormMarkEmpty = "ReportTemplate.Form.MarkEmpty";

    public const string FormMarkText = "ReportTemplate.Form.MarkText";

    public const string FormLateMark = "ReportTemplate.Form.LateMark";

    public const string FormLateHidden = "ReportTemplate.Form.LateHidden";

    /// <summary>US-042 FR-010: the setting "names" and its two values, on the form and on the report's switch.</summary>
    public const string FormNames = "ReportTemplate.Form.Names";

    public const string NameSourceProfile = "ReportTemplate.NameSource.Profile";

    public const string NameSourceEmail = "ReportTemplate.NameSource.Email";

    public const string FormSave = "ReportTemplate.Form.Save";

    public const string FormCancel = "ReportTemplate.Form.Cancel";

    /// <summary>Prefix of a scale message that names its row ("Row {0}:"); the message sentence itself has no placeholder.</summary>
    public const string FormErrorRow = "ReportTemplate.Validation.Row";

    public const string DeleteTitle = "ReportTemplate.Delete.Title";

    public const string DeletePrompt = "ReportTemplate.Delete.Prompt";

    public const string DeleteConfirm = "ReportTemplate.Delete.Confirm";

    public const string ReportTitle = "Report.Title";

    public const string ReportTemplateLabel = "Report.Form.Template";

    public const string ReportGrading = "Report.Part.Grading";

    public const string ReportLessonTopics = "Report.Part.LessonTopics";

    public const string ReportTeachers = "Report.Header.Teachers";

    public const string ReportPeriod = "Report.Header.Period";

    public const string ReportDate = "Report.Lesson.Date";

    public const string ReportTopic = "Report.Lesson.Topic";

    public const string ReportHours = "Report.Lesson.Hours";

    public const string ReportTeacher = "Report.Lesson.Teacher";

    public const string ReportIndependentWork = "Report.Lesson.IndependentWork";

    public const string ReportSignature = "Report.Lesson.Signature";

    public const string NoStudents = "Report.Empty.NoStudents";

    public const string TeacherUnnamed = "Report.Teacher.Unnamed";

    /// <summary>US-042 FR-010: the caption of the report page's name-source switch.</summary>
    public const string ReportNameSwitch = "Report.NameSwitch.Caption";

    public static string NameSource(ReportNameSource source) =>
        source == ReportNameSource.Email ? NameSourceEmail : NameSourceProfile;

    public static string Validation(ReportTemplateFieldErrorKey key) => "ReportTemplate.Validation." + key;

    public static string Reference(ReportTemplateReferenceMessageKey key) => "ReportTemplate.Reference." + key;

    public static string Confirmation(ReportTemplateConfirmationKey key) => "ReportTemplate.Confirmation." + key;

    public static string Empty(ReportEmptyStateKey key) => "Report.Empty." + key;

    /// <summary>
    /// Api-design §2.9: the template messages are this Story's, the course and period messages are US-025's; the
    /// name-source message is US-042's.
    /// </summary>
    public static string Message(ReportMessageKey key) => key switch
    {
        ReportMessageKey.TemplateMalformed => Reference(ReportTemplateReferenceMessageKey.TemplateMalformed),
        ReportMessageKey.TemplateNotFound => Reference(ReportTemplateReferenceMessageKey.TemplateNotFound),
        ReportMessageKey.NameSourceMalformed => "Report.Validation." + key,
        _ => "Journal.Validation." + key,
    };

    /// <summary>Api-design §2.9: a program mark is a <c>ReportCellState</c> member or <c>Late</c>; both reuse US-025 keys.</summary>
    public static string ProgramMark(string? programKey) =>
        programKey == "Late" ? JournalTextKeys.Late : "Journal.Cell." + programKey;
}
