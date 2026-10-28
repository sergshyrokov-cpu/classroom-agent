namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the data-health banner and the Admin's confirmation (US-062 api-design §2.3, §3; DC-10). Account ids,
/// counts, rule names and exception type names only — never a course name or id from the request, a student name, a
/// grade or an exception message (SC-10).
/// </summary>
public static partial class DataHealthLog
{
    [LoggerMessage(
        EventId = 5620,
        EventName = "DataHealthBannerFailed",
        Level = LogLevel.Warning,
        Message = "The data-health banner was not computed: {ExceptionType}")]
    public static partial void BannerFailed(ILogger logger, string exceptionType);

    [LoggerMessage(
        EventId = 5621,
        EventName = "CoursesConfirmedDeleted",
        Level = LogLevel.Information,
        Message = "Account {AppUserId} confirmed {CourseCount} courses deleted in Classroom")]
    public static partial void CoursesConfirmedDeleted(ILogger logger, long appUserId, int courseCount);

    [LoggerMessage(
        EventId = 5622,
        EventName = "CourseDeletionConfirmationRefused",
        Level = LogLevel.Warning,
        Message = "A confirmation of deleted courses by account {AppUserId} was refused: {Rule}")]
    public static partial void ConfirmationRefused(ILogger logger, long appUserId, string rule);
}
