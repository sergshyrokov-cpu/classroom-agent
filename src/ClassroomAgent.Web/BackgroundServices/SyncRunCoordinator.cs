namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// Guarantees at most one synchronization run at a time (US-013 spec FR-004, OD-007): a scheduled run, or a run
/// requested out of schedule by US-019, never starts while another is already in progress. The decision and the
/// start are one atomic step, so two callers never start two runs. Everything is process state — a restart
/// starts from the schedule again.
/// </summary>
/// <remarks>
/// A request arriving during a run is <b>remembered</b>, once however many arrive, and becomes startable as soon
/// as the running one finishes. The shape follows <see cref="PushCheckCoordinator"/>, which does the same job
/// for US-006, without its minimum interval: a synchronization request carries no such rule.
/// <para>
/// US-037 spec FR-014, OD-002: the retention purge takes the same gate, so a purge and a synchronization run never
/// overlap. <see cref="IsRunning"/> keeps meaning "a synchronization run" — readiness reads it (API design §3).
/// </para>
/// </remarks>
public sealed class SyncRunCoordinator
{
    private readonly Lock _gate = new();
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _running;
    private bool _requested;
    private bool _purging;

    /// <summary>A task that completes the next time the state changes.</summary>
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

    /// <summary>Whether a run is in progress right now.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    /// <summary>US-037 spec FR-014: whether the retention purge holds the gate right now.</summary>
    public bool IsPurging
    {
        get
        {
            lock (_gate)
            {
                return _purging;
            }
        }
    }

    /// <summary>Whether an out-of-schedule request is remembered and waiting to start.</summary>
    public bool IsRequested
    {
        get
        {
            lock (_gate)
            {
                return _requested && !_running && !_purging;
            }
        }
    }

    /// <summary>The schedule says a run is due: it begins here, unless one is already in progress.</summary>
    public bool TryStartScheduledRun() => TryStart(clearRequest: false);

    /// <summary>A remembered out-of-schedule request begins here, unless a run is already in progress.</summary>
    public bool TryStartRequestedRun()
    {
        lock (_gate)
        {
            if (_running || _purging || !_requested)
            {
                return false;
            }

            _requested = false;
            _running = true;
            Signal();
            return true;
        }
    }

    /// <summary>
    /// The out-of-schedule entry point US-019 will call. The request is always accepted — it is remembered and
    /// started by the service, which is what AD-5's "enqueue and return immediately" means.
    /// </summary>
    public bool Request()
    {
        lock (_gate)
        {
            _requested = true;
            Signal();
            return true;
        }
    }

    /// <summary>The run that was started has finished, whatever its outcome.</summary>
    public void RunCompleted()
    {
        lock (_gate)
        {
            _running = false;
            Signal();
        }
    }

    /// <summary>
    /// US-037 spec FR-014: the purge begins here unless a synchronization run is in progress; while it runs, no
    /// synchronization run starts, and a request made meanwhile stays remembered.
    /// </summary>
    public bool TryStartPurge()
    {
        lock (_gate)
        {
            if (_running || _purging)
            {
                return false;
            }

            _purging = true;
            Signal();
            return true;
        }
    }

    /// <summary>US-037 spec FR-014: the purge that was started has finished, whatever its outcome.</summary>
    public void PurgeCompleted()
    {
        lock (_gate)
        {
            _purging = false;
            Signal();
        }
    }

    private bool TryStart(bool clearRequest)
    {
        lock (_gate)
        {
            if (_running || _purging)
            {
                return false;
            }

            if (clearRequest)
            {
                _requested = false;
            }

            _running = true;
            Signal();
            return true;
        }
    }

    private void Signal()
    {
        var changed = _changed;
        _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        changed.TrySetResult();
    }
}
