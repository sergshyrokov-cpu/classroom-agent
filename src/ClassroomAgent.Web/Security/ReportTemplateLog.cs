namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the report templates and the report page (US-027 spec FR-019; DC-10). Internal identifiers,
/// actions, field names, rules and counts only — never a template name, mark or label, a name, an email, a grade, a
/// title or an entered value (SC-10, NFR-023).
/// </summary>
public static partial class ReportTemplateLog
{
    [LoggerMessage(
        EventId = 5260,
        EventName = "ReportTemplateWritten",
        Level = LogLevel.Information,
        Message = "Report template {TemplateId} {Action} by account {AppUserId}")]
    public static partial void Written(ILogger logger, long templateId, string action, long appUserId);

    /// <summary>Spec FR-019: the field and the rule, as the error keys name them — never the value.</summary>
    [LoggerMessage(
        EventId = 5261,
        EventName = "ReportTemplateRejected",
        Level = LogLevel.Warning,
        Message = "Report template form rejected in request {RequestId}: {Rules}")]
    public static partial void Rejected(ILogger logger, string? requestId, string rules);

    [LoggerMessage(
        EventId = 5262,
        EventName = "ReportTemplateRefused",
        Level = LogLevel.Warning,
        Message = "Report template request refused in request {RequestId}: {Outcome}")]
    public static partial void Refused(ILogger logger, string? requestId, string outcome);

    [LoggerMessage(
        EventId = 5263,
        EventName = "ReportBuilt",
        Level = LogLevel.Information,
        Message = "Report of template {Template} for course {CourseId} for {From}..{To} built for account {AppUserId}: {RowCount} rows, {ColumnCount} columns, {TopicCount} topics, names from {NameSource} chosen by {NameSourceOrigin}")]
    public static partial void Built(
        ILogger logger,
        string template,
        long courseId,
        DateOnly? from,
        DateOnly? to,
        long appUserId,
        int rowCount,
        int columnCount,
        int topicCount,
        string nameSource,
        string nameSourceOrigin);

    [LoggerMessage(
        EventId = 5264,
        EventName = "ReportQueryRefused",
        Level = LogLevel.Warning,
        Message = "Report query refused in request {RequestId}: {Rules}")]
    public static partial void QueryRefused(ILogger logger, string? requestId, string rules);
}
