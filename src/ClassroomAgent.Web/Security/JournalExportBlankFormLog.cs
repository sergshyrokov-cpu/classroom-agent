namespace ClassroomAgent.Web.Security;

/// <summary>
/// US-043 spec FR-011: the success line of an export into the group-lessons blank form — the US-028 line under the same
/// event name, plus the layout, the fill method, the per-copy sizes and the number of sheets. Codes and counts only (SC-10).
/// Its own class because one class may not log two methods under one event name.
/// </summary>
public static partial class JournalExportBlankFormLog
{
    [LoggerMessage(
        EventId = 5272,
        EventName = "JournalExported",
        Level = LogLevel.Information,
        Message = "Journal of template {Template} for course {CourseId} for {From}..{To} exported by account {AppUserId}: names from {NameSource} chosen by {NameSourceOrigin}, {RowCount} rows, {TopicCount} topics, {FileBytes} bytes, layout {Layout} filled by method {FillMethod} with {StudentsPerCopy} students and {LessonsPerCopy} lessons per copy into {SheetCount} sheets")]
    public static partial void ExportedToBlankForm(
        ILogger logger,
        string template,
        long courseId,
        DateOnly from,
        DateOnly to,
        long appUserId,
        string nameSource,
        string nameSourceOrigin,
        int rowCount,
        int topicCount,
        int fileBytes,
        string layout,
        string fillMethod,
        int studentsPerCopy,
        int lessonsPerCopy,
        int sheetCount);
}
