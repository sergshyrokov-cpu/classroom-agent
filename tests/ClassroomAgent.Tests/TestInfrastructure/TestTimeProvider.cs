namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>A clock the test advances by hand (lockout, session lifetime, audit time).</summary>
public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock _gate = new();
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now += by;
        }
    }
}
