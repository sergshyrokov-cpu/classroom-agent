using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of a manual synchronization request (US-019 spec FR-010; DC-10). Internal identifiers and
/// categories only — never an email, a domain or anything from Google (SC-10).
/// </summary>
public static partial class SynchronizationRequestLog
{
    [LoggerMessage(
        EventId = 5140,
        EventName = "SynchronizationRequested",
        Level = LogLevel.Information,
        Message = "Synchronization requested by account {AppUserId} in request {RequestId}: {Outcome}")]
    public static partial void Requested(ILogger logger, long appUserId, string? requestId, RequestSynchronizationOutcome outcome);

    [LoggerMessage(
        EventId = 5141,
        EventName = "SynchronizationRequestRefused",
        Level = LogLevel.Warning,
        Message = "Synchronization request refused in request {RequestId}: {Category}")]
    public static partial void Refused(ILogger logger, string? requestId, string category);
}
