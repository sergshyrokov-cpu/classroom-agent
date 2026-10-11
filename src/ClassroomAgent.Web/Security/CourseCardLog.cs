namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the course card (US-021 spec FR-013; DC-10). Internal identifiers and counts — never a course name,
/// section, description, room, a person's name, email, Google id or the requested value (SC-10, spec S-07).
/// </summary>
public static partial class CourseCardLog
{
    /// <summary>The event id of <see cref="Refused"/>; its line names the route, never the refused value in the path (US-045).</summary>
    public const int RefusedEventId = 5463;

    [LoggerMessage(
        EventId = 5462,
        EventName = "CourseCardShown",
        Level = LogLevel.Information,
        Message = "Course card shown for account {AppUserId}: course {CourseId}, {TeacherCount} teachers, {StudentCount} students, {LeaverCount} leavers")]
    public static partial void Shown(
        ILogger logger, long appUserId, long courseId, int teacherCount, int studentCount, int leaverCount);

    /// <summary>Spec FR-013, AC-008: the parameter and the outcome, never the value.</summary>
    [LoggerMessage(
        EventId = RefusedEventId,
        EventName = "CourseCardRefused",
        Level = LogLevel.Warning,
        Message = "Course card refused in request {RequestId}: parameter {Parameter}, outcome {Outcome}")]
    public static partial void Refused(ILogger logger, string? requestId, string parameter, string outcome);
}
