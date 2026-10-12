namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the teacher list (US-022 spec FR-016, api-design §2.9; DC-10). The actor's id, the effective status,
/// the page and the total — never a teacher name, email or a rejected value (SC-10, S-08).
/// </summary>
public static partial class TeacherListLog
{
    [LoggerMessage(
        EventId = 5464,
        EventName = "TeacherListShown",
        Level = LogLevel.Information,
        Message = "Teacher list shown for account {AppUserId}: status {Status}, page {Page}, {TotalCount} listed teachers")]
    public static partial void Shown(ILogger logger, long appUserId, string status, int page, int totalCount);

    /// <summary>Spec FR-016: one line per rejected parameter — the parameter and the rule, never the value.</summary>
    [LoggerMessage(
        EventId = 5465,
        EventName = "TeacherListQueryRefused",
        Level = LogLevel.Warning,
        Message = "Teacher list query refused in request {RequestId}: parameter {Parameter}, rule {Rule}")]
    public static partial void Refused(ILogger logger, string? requestId, string parameter, string rule);
}
