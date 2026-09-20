using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The log lines of the connection settings (US-009 spec FR-014). Every line carries the account id, the
/// request id and a category — never the address typed, never the domain, never the rejected body (SC-10).
/// </summary>
public static class WorkspaceConnectionLog
{
    private static readonly EventId SavedEvent = new(2300, "WorkspaceConnectionSaved");

    private static readonly EventId RefusedEvent = new(2301, "WorkspaceConnectionSaveRefused");

    private static readonly EventId RejectedEvent = new(2302, "WorkspaceConnectionRequestRejected");

    public static void Saved(ILogger logger, long appUserId, string? requestId) =>
        logger.LogInformation(
            SavedEvent,
            "Workspace connection saved by account {AppUserId} in request {RequestId}",
            appUserId,
            requestId);

    public static void Refused(ILogger logger, SaveWorkspaceConnectionRefusal refusal, string? requestId) =>
        logger.LogWarning(
            RefusedEvent,
            "Workspace connection save refused in request {RequestId}: {Refusal}",
            requestId,
            refusal);

    /// <summary>A malformed request: the category only, never the value that was rejected (SC-10).</summary>
    public static void Rejected(ILogger logger, string? requestId) =>
        logger.LogWarning(
            RejectedEvent,
            "Workspace connection save rejected in request {RequestId}: the request failed validation",
            requestId);
}
