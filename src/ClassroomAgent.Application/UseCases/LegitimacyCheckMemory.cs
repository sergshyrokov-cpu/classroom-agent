using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// What the process remembers between checks and nothing persists (US-005 spec I-3): the result of the last
/// check since startup — for readiness (FR-013) — and the last read-only determination — for logging a mode
/// change once (FR-010). One instance per process.
/// </summary>
public sealed class LegitimacyCheckMemory
{
    private readonly Lock _gate = new();
    private CheckOutcome? _lastOutcome;
    private bool? _lastReadOnly;

    /// <summary>The result of the last check since startup; null before the first one completes.</summary>
    public CheckOutcome? LastOutcome
    {
        get
        {
            lock (_gate)
            {
                return _lastOutcome;
            }
        }
    }

    /// <summary>Stores the result of a completed check and returns the previous one.</summary>
    public CheckOutcome? RememberOutcome(CheckOutcome outcome)
    {
        lock (_gate)
        {
            var previous = _lastOutcome;
            _lastOutcome = outcome;
            return previous;
        }
    }

    /// <summary>Stores a read-only determination and returns the previous one; null when none was made yet.</summary>
    public bool? RememberReadOnly(bool isReadOnly)
    {
        lock (_gate)
        {
            var previous = _lastReadOnly;
            _lastReadOnly = isReadOnly;
            return previous;
        }
    }
}
