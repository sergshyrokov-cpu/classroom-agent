using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the journal page (US-025 spec FR-016; DC-10). Internal identifiers, dates and counts only — never
/// a name, an email, a grade, a title or a rejected value (SC-10, NFR-023).
/// </summary>
public static partial class JournalLog
{
    [LoggerMessage(
        EventId = 5250,
        EventName = "JournalBuilt",
        Level = LogLevel.Information,
        Message = "Journal of course {CourseId} for {From}..{To} ({View}) built for account {AppUserId}: {RowCount} rows, {ColumnCount} columns")]
    public static partial void Built(
        ILogger logger, long courseId, DateOnly? from, DateOnly? to, JournalView view, long appUserId, int rowCount, int columnCount);

    /// <summary>Spec FR-008: the parameter and the rule, as the message keys name them — never the value.</summary>
    [LoggerMessage(
        EventId = 5251,
        EventName = "JournalQueryRefused",
        Level = LogLevel.Warning,
        Message = "Journal query refused in request {RequestId}: {Rules}")]
    public static partial void Refused(ILogger logger, string? requestId, string rules);
}
