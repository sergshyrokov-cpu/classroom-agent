namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the Meet reports pages (US-033 spec FR-013, §9; DC-10). Internal identifiers, dates and counts
/// only — never a name, an email, a meeting code or a rejected value (SC-10, db-design §6).
/// </summary>
public static partial class MeetReportsLog
{
    [LoggerMessage(
        EventId = 5280,
        EventName = "MeetReportsBuilt",
        Level = LogLevel.Information,
        Message = "Meet reports of course {CourseId} for {From}..{To} built for account {AppUserId}: {MeetingCount} meetings, {StudentCount} students")]
    public static partial void Built(
        ILogger logger, long courseId, DateOnly? from, DateOnly? to, long appUserId, int meetingCount, int studentCount);

    /// <summary>Spec FR-013: the parameters and the rules, as the message keys name them — never the values.</summary>
    [LoggerMessage(
        EventId = 5281,
        EventName = "MeetReportsQueryRefused",
        Level = LogLevel.Warning,
        Message = "Meet reports query refused in request {RequestId}: parameters {Parameters}, rules {Rules}")]
    public static partial void Refused(ILogger logger, string? requestId, string parameters, string rules);

    [LoggerMessage(
        EventId = 5282,
        EventName = "MeetReportMeetingBuilt",
        Level = LogLevel.Information,
        Message = "Meet report of meeting {MeetingId} of course {CourseId} built for account {AppUserId}: {LineCount} lines")]
    public static partial void MeetingBuilt(ILogger logger, long meetingId, long courseId, long appUserId, int lineCount);

    /// <summary>Report 4 refused (api-design §5): the parameter and the rule — never the value.</summary>
    [LoggerMessage(
        EventId = 5283,
        EventName = "MeetReportMeetingRefused",
        Level = LogLevel.Warning,
        Message = "Meet report meeting query refused in request {RequestId}: parameters {Parameters}, rule {Rule}")]
    public static partial void MeetingRefused(ILogger logger, string? requestId, string parameters, string rule);
}
