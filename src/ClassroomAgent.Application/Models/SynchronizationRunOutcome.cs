namespace ClassroomAgent.Application.Models;

/// <summary>
/// What a synchronization run did (US-013 spec FR-005, FR-014): ran with a run identifier, a processed count and
/// an optional error, or was skipped with its reason — read-only mode with the BR-025 cause, or an unusable
/// connection with its state.
/// </summary>
/// <remarks>
/// Compile-only skeleton created at TEST_WRITING under US-013 OD-008; IMPLEMENTATION owns it from here.
/// </remarks>
public sealed class SynchronizationRunOutcome
{
    private SynchronizationRunOutcome(
        Guid? runId,
        int? processedCount,
        string? error,
        LegitimacyModeReason? readOnlyReason,
        WorkspaceConnectionState? connectionState)
    {
        RunId = runId;
        ProcessedCount = processedCount;
        Error = error;
        ReadOnlyReason = readOnlyReason;
        ConnectionState = connectionState;
    }

    public Guid? RunId { get; }

    public int? ProcessedCount { get; }

    public string? Error { get; }

    public LegitimacyModeReason? ReadOnlyReason { get; }

    public WorkspaceConnectionState? ConnectionState { get; }

    public bool Failed => Error is not null;

    public static SynchronizationRunOutcome Ran(Guid runId, int processedCount, string? error) =>
        new(runId, processedCount, error, null, null);

    public static SynchronizationRunOutcome SkippedReadOnly(LegitimacyModeReason reason) =>
        new(null, null, null, reason, null);

    public static SynchronizationRunOutcome SkippedConnection(WorkspaceConnectionState state) =>
        new(null, null, null, null, state);
}
