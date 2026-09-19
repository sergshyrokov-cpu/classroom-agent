namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// What the push receiver and the check scheduler share (US-006 spec FR-010; api-design §5;
/// <c>trebovaniya.md</c> v77). A push starts a check only when no check is running and at least a minute has
/// passed since the start of the previous push-triggered check; otherwise it is accepted and leaves a single
/// pending check, which starts as soon as both hold again. The decision and the start are one atomic step, so
/// two pushes never start two checks. Everything is process state: a restart runs the startup check anyway.
/// </summary>
public sealed class PushCheckCoordinator(TimeProvider timeProvider)
{
    /// <summary>Push-triggered checks start at most this often (spec FR-010, I-11).</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _running;
    private bool _pending;
    private bool _started;
    private DateTimeOffset? _lastPushCheckStartedAt;

    /// <summary>When the remembered pending check may start; null when none is remembered.</summary>
    public DateTimeOffset? PendingDueAt
    {
        get
        {
            lock (_gate)
            {
                return !_pending ? null : EligibleAt();
            }
        }
    }

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

    /// <summary>
    /// A push arrived. Either a check begins right here — the caller answers <c>202</c> and the scheduler runs
    /// it — or one pending check is remembered, however many such pushes arrive.
    /// </summary>
    public PushCheckRequestResult Request()
    {
        PushCheckRequestResult result;
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (!_running && now >= EligibleAt())
            {
                BeginPushCheck(now);
                _started = true;
                result = PushCheckRequestResult.Started;
            }
            else
            {
                _pending = true;
                result = PushCheckRequestResult.Deferred;
            }
        }

        Signal();
        return result;
    }

    /// <summary>Takes the check a <see cref="Request"/> already began, so the scheduler runs exactly that one.</summary>
    public bool TryTakeStartedCheck()
    {
        lock (_gate)
        {
            if (!_started)
            {
                return false;
            }

            _started = false;
            return true;
        }
    }

    /// <summary>Begins the remembered pending check when nothing runs and the minute has passed.</summary>
    public bool TryStartPendingCheck()
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (!_pending || _running || now < EligibleAt())
            {
                return false;
            }

            _pending = false;
            BeginPushCheck(now);
            return true;
        }
    }

    /// <summary>Begins a scheduled check; refused while another check is running.</summary>
    public bool TryStartScheduledCheck()
    {
        lock (_gate)
        {
            if (_running)
            {
                return false;
            }

            _running = true;
            return true;
        }
    }

    /// <summary>The running check has finished, whatever its outcome.</summary>
    public void CheckCompleted()
    {
        lock (_gate)
        {
            _running = false;
        }

        Signal();
    }

    /// <summary>When a push-triggered check may next start; a scheduled check does not start the minute (spec I-11).</summary>
    private DateTimeOffset EligibleAt() =>
        _lastPushCheckStartedAt is { } started ? started + MinimumInterval : DateTimeOffset.MinValue;

    private void BeginPushCheck(DateTimeOffset now)
    {
        _running = true;
        _lastPushCheckStartedAt = now;
    }

    private void Signal()
    {
        TaskCompletionSource previous;
        lock (_gate)
        {
            previous = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        previous.TrySetResult();
    }
}
