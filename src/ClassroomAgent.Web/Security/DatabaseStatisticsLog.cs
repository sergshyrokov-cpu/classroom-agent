namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log line of the Database page (US-024 spec FR-013; DC-10): the actor's id only — no counter that names anything,
/// no meeting code, no request value (SC-10, S-10). The press logs as US-019 does (<see cref="SynchronizationRequestLog"/>).
/// </summary>
public static partial class DatabaseStatisticsLog
{
    [LoggerMessage(
        EventId = 5466,
        EventName = "DatabaseStatisticsShown",
        Level = LogLevel.Information,
        Message = "Database statistics shown for account {AppUserId}")]
    public static partial void Shown(ILogger logger, long appUserId);
}
