using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The startup self-check (US-011 spec FR-010; DC-5): the same eight steps as "Проверить доступ", run once when the
/// installation starts, so the Owner can confirm a rotated key from the log without the school. It writes nothing —
/// no audit row, no table — and in read-only mode it does not run (BR-026, SC-5).
/// </summary>
/// <remarks>
/// Read-only mode is decided by the same guard every use case calls, from the stored <c>LegitimacyState</c>
/// (spec I-7); a refusal becomes a skip with its reason, not an error (OD-004).
/// </remarks>
public sealed class RunStartupSelfCheckUseCase(
    IGoogleAccessProbe probe,
    GetWorkspaceConnectionQuery connectionQuery,
    IReadOnlyModeGuard readOnlyMode,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only refusal carries (US-007 spec VR-001).</summary>
    public const string Operation = "AccessCheck.StartupSelfCheck";

    public async Task<StartupSelfCheckOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException refusal)
        {
            return StartupSelfCheckOutcome.SkippedReadOnly(refusal.Reason);
        }

        var view = await connectionQuery.ExecuteAsync(cancellationToken);
        if (!view.IsUsable || view.SavedImpersonationUserEmail is not { } technicalAccount)
        {
            return StartupSelfCheckOutcome.SkippedConnection(view.State);
        }

        return StartupSelfCheckOutcome.Ran(
            await AccessCheckSteps.RunAsync(probe, technicalAccount, timeProvider, cancellationToken));
    }
}
