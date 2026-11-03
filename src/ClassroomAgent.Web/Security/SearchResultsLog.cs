namespace ClassroomAgent.Web.Security;

/// <summary>
/// The results page's log lines (US-071 openapi <c>x-log</c>, api-design D-10): counts or the parameter's name, never the
/// searched text (SC-10, SC-14).
/// </summary>
public static partial class SearchResultsLog
{
    [LoggerMessage(
        EventId = 5623,
        EventName = "SearchResultsShown",
        Level = LogLevel.Information,
        Message = "Search results shown for account {AppUserId}: {Students} students, {Teachers} teachers, {Courses} courses, {MeetCodes} Meet codes")]
    public static partial void Shown(ILogger logger, long appUserId, int students, int teachers, int courses, int meetCodes);

    /// <summary>VR-002: the parameter and the rule, never the value.</summary>
    [LoggerMessage(
        EventId = 5624,
        EventName = "SearchQueryRefused",
        Level = LogLevel.Warning,
        Message = "Search query refused in request {RequestId}: parameter {Parameter}, rule {Rule}")]
    public static partial void Refused(ILogger logger, string? requestId, string parameter, string rule);
}
