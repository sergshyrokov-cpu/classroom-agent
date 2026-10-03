namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The random factor applied to each nominal retry pause, in [0.8, 1.2] (US-017 spec FR-002, FR-003, I-2). Injected so
/// a test can fix it.
/// </summary>
public interface IGoogleRetryJitter
{
    double NextFactor();
}
