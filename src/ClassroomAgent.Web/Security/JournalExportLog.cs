namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the journal export (US-028 spec FR-012; api-design §3; DC-10). Internal ids, the period, codes and
/// counts only — never a name, an email, a grade, a title, a template text, a rejected value or any file content (SC-10).
/// </summary>
public static partial class JournalExportLog
{
    [LoggerMessage(
        EventId = 5270,
        EventName = "JournalExported",
        Level = LogLevel.Information,
        Message = "Journal of template {Template} for course {CourseId} for {From}..{To} exported by account {AppUserId}: names from {NameSource} chosen by {NameSourceOrigin}, {RowCount} rows, {TopicCount} topics, {FileBytes} bytes")]
    public static partial void Exported(
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
        int fileBytes);

    /// <summary>Spec FR-012: the field and the rule of each failure — never the value.</summary>
    [LoggerMessage(
        EventId = 5271,
        EventName = "JournalExportRefused",
        Level = LogLevel.Warning,
        Message = "Journal export refused in request {RequestId} with {Status}: {Rules}")]
    public static partial void Refused(ILogger logger, string? requestId, int status, string rules);
}
