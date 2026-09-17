namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// A clock whose time and timers move only when the test advances it (US-005 test strategy §3).
/// Timers created through it — <c>Task.Delay(TimeSpan, TimeProvider, …)</c>,
/// <c>CancellationTokenSource(TimeSpan, TimeProvider)</c>, <c>PeriodicTimer</c> — fire synchronously
/// inside <see cref="Advance"/>, in due order, so the check schedule and the call timeout are tested
/// without real waiting.
/// </summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    /// <summary>How long a test waits in real time for the host to reach an expected state.</summary>
    public static readonly TimeSpan RealTimeLimit = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = start;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <summary>The due times of the timers currently scheduled.</summary>
    public IReadOnlyList<DateTimeOffset> PendingDueTimes
    {
        get
        {
            lock (_gate)
            {
                return _timers.Where(t => t.DueAt is not null).Select(t => t.DueAt!.Value).Order().ToList();
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves time forward, firing every timer that falls due on the way.</summary>
    public void Advance(TimeSpan by)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + by;
        }

        while (true)
        {
            ManualTimer? next;
            lock (_gate)
            {
                next = _timers
                    .Where(t => t.DueAt is not null && t.DueAt <= target)
                    .OrderBy(t => t.DueAt)
                    .FirstOrDefault();
                if (next is null)
                {
                    _now = target;
                    break;
                }

                _now = next.DueAt!.Value;
                next.Reschedule(_now);
            }

            next.Fire();
            Signal();
        }

        Signal();
    }

    /// <summary>Waits (in real time, bounded) until a timer is scheduled for exactly that instant.</summary>
    public Task WaitForTimerAtAsync(DateTimeOffset dueAt, CancellationToken cancellationToken) =>
        WaitUntilAsync(() => PendingDueTimes.Contains(dueAt), $"a timer due at {dueAt:O}", cancellationToken);

    /// <summary>Waits (in real time, bounded) until the condition holds; fails the test otherwise.</summary>
    public async Task WaitUntilAsync(Func<bool> condition, string description, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(RealTimeLimit);
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                changed = _changed.Task;
            }

            if (condition())
            {
                return;
            }

            try
            {
                await changed.WaitAsync(TimeSpan.FromMilliseconds(100), limit.Token);
            }
            catch (TimeoutException)
            {
                // Re-check: state outside the clock (a database write) may have changed.
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var pending = string.Join(", ", PendingDueTimes.Select(d => d.ToString("O")));
                Assert.Fail($"Timed out waiting for {description}. Now {GetUtcNow():O}; pending timers: [{pending}].");
            }
        }
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

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (!owner._timers.Contains(this))
                {
                    owner._timers.Add(this);
                }

                _period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
            }

            owner.Signal();
            return true;
        }

        public void Reschedule(DateTimeOffset firedAt) =>
            DueAt = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : firedAt + _period;

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (owner._gate)
            {
                owner._timers.Remove(this);
            }

            owner.Signal();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
