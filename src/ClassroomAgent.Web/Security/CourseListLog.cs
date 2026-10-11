namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the course list (US-020 spec FR-018; DC-10). Internal identifiers, the effective status, counts and
/// whether a search was given — never a course name, section, teacher name, email, the search text or a rejected value
/// (SC-10, spec I-4).
/// </summary>
public static partial class CourseListLog
{
    [LoggerMessage(
        EventId = 5460,
        EventName = "CourseListShown",
        Level = LogLevel.Information,
        Message = "Course list shown for account {AppUserId}: status {Status}, teacher {TeacherId}, search given {SearchGiven}, page {Page}, {TotalCount} matching courses")]
    public static partial void Shown(
        ILogger logger, long appUserId, string status, long? teacherId, bool searchGiven, int page, int totalCount);

    /// <summary>Spec FR-018: one line per rejected parameter — the parameter and the rule, never the value.</summary>
    [LoggerMessage(
        EventId = 5461,
        EventName = "CourseListQueryRefused",
        Level = LogLevel.Warning,
        Message = "Course list query refused in request {RequestId}: parameter {Parameter}, rule {Rule}")]
    public static partial void Refused(ILogger logger, string? requestId, string parameter, string rule);
}
