namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Records whether the Google port was reached. In read-only mode it must record nothing at all: the
/// refusal happens before the port is called, so nothing leaves the process (US-007 AC-006, SC-5, SC-8).
/// No test calls a real Google API (TC-4).
/// </summary>
public sealed class SyntheticGooglePort : ISyntheticGooglePort
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task ReadAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Task.CompletedTask;
    }
}
