namespace ClassroomAgent.Application.Models;

/// <summary>
/// What the startup self-check did (US-011 spec FR-010, OD-004): ran with a result, or was skipped with its reason
/// — read-only mode with the BR-025 cause, or an unusable connection with its state.
/// </summary>
public sealed class StartupSelfCheckOutcome
{
    private StartupSelfCheckOutcome(
        AccessCheckResult? result,
        LegitimacyModeReason? readOnlyReason,
        WorkspaceConnectionState? connectionState)
    {
        Result = result;
        ReadOnlyReason = readOnlyReason;
        ConnectionState = connectionState;
    }

    public AccessCheckResult? Result { get; }

    public LegitimacyModeReason? ReadOnlyReason { get; }

    public WorkspaceConnectionState? ConnectionState { get; }

    public static StartupSelfCheckOutcome Ran(AccessCheckResult result) => new(result, null, null);

    public static StartupSelfCheckOutcome SkippedReadOnly(LegitimacyModeReason reason) => new(null, reason, null);

    public static StartupSelfCheckOutcome SkippedConnection(WorkspaceConnectionState state) => new(null, null, state);
}
