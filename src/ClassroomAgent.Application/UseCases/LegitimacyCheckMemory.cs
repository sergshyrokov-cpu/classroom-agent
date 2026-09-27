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
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CheckOutcome? _lastOutcome;
    private bool? _lastReadOnly;

    /// <summary>
    /// A task that completes the next time a check result is remembered. US-013 spec FR-011 waits on it: the
    /// synchronization service must not start its first run before the first legitimacy determination, and
    /// polling would need the clock to move, which a test drives by hand (spec I-5).
    /// </summary>
    public Task Changed
    {
        get
        {
            lock (_gate)
            {
                return _changed.Task;
            }
        }
    }

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
            Signal();
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

    private void Signal()
    {
        var changed = _changed;
        _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        changed.TrySetResult();
    }
}
