namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the Meet export (US-044 spec FR-014; DC-10). Internal ids, the period or the meeting, counts and the
/// orientation only — never a name, an email, a meeting code, a rejected value or any file content (SC-10).
/// </summary>
public static partial class MeetExportLog
{
    [LoggerMessage(
        EventId = 5440,
        EventName = "MeetReportsExported",
        Level = LogLevel.Information,
        Message = "Meet reports of course {CourseId} for {From}..{To} exported by account {AppUserId}: {StudentRows} student rows, {MeetingRows} meeting rows, orientation {Orientation}, {FileBytes} bytes")]
    public static partial void ReportsExported(
        ILogger logger,
        long courseId,
        string from,
        string to,
        long appUserId,
        int studentRows,
        int meetingRows,
        string orientation,
        int fileBytes);

    [LoggerMessage(
        EventId = 5441,
        EventName = "MeetMeetingExported",
        Level = LogLevel.Information,
        Message = "Meet meeting {MeetingId} of course {CourseId} exported by account {AppUserId}: {PersonRows} person rows, orientation {Orientation}, {FileBytes} bytes")]
    public static partial void MeetingExported(
        ILogger logger,
        long meetingId,
        long courseId,
        long appUserId,
        int personRows,
        string orientation,
        int fileBytes);

    /// <summary>Spec FR-014: the field and the rule of each failure — never the value.</summary>
    [LoggerMessage(
        EventId = 5442,
        EventName = "MeetExportRefused",
        Level = LogLevel.Warning,
        Message = "Meet export refused in request {RequestId} with {Status}: {Rules}")]
    public static partial void Refused(ILogger logger, string? requestId, int status, string rules);
}
