using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Reads the installation's <c>SyncState</c> for the connection page (US-017 spec FR-007, db-design §4). Writes
/// nothing; permitted in read-only mode.
/// </summary>
/// <remarks>
/// A row that does not exist is "never run" (US-013 spec I-2). The stored <c>last_error</c> is shown only for a
/// failed run and only when it is exactly the name of a declared <see cref="SyncDiagnosis"/>; anything else — a
/// value an earlier version left, another casing, numeric text — is shown as <see cref="SyncDiagnosis.Unexpected"/>
/// (spec FR-006, VR-002, I-6). <see cref="Enum.TryParse{TEnum}(string?, out TEnum)"/> is deliberately not used: it
/// accepts numbers and ignores padding.
/// </remarks>
public sealed class GetLastSynchronizationQuery(ISyncStateRepository states)
{
    public async Task<LastSynchronizationView> ExecuteAsync(CancellationToken cancellationToken)
    {
        var state = await states.GetForReadAsync(cancellationToken);
        if (state is null)
        {
            return new LastSynchronizationView(LastSynchronizationStatus.NeverRun, null, null, null, null);
        }

        var status = state.Status switch
        {
            SyncRunStatus.Running => LastSynchronizationStatus.Running,
            SyncRunStatus.Completed => LastSynchronizationStatus.Completed,
            SyncRunStatus.Failed => LastSynchronizationStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Status, null),
        };

        return new LastSynchronizationView(
            status,
            state.StartedAt,
            state.FinishedAt,
            state.LastSuccessfulRunAt,
            status == LastSynchronizationStatus.Failed ? DiagnosisOf(state) : null);
    }

    private static SyncDiagnosis DiagnosisOf(SyncState state)
    {
        foreach (var name in Enum.GetNames<SyncDiagnosis>())
        {
            if (string.Equals(name, state.LastError, StringComparison.Ordinal))
            {
                return Enum.Parse<SyncDiagnosis>(name);
            }
        }

        return SyncDiagnosis.Unexpected;
    }
}
