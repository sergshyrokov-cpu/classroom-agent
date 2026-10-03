namespace ClassroomAgent.Infrastructure.Google;

/// <summary>The production jitter: a uniformly random factor in [0.8, 1.2] (US-017 spec FR-002, I-2).</summary>
public sealed class RandomGoogleRetryJitter : IGoogleRetryJitter
{
    public double NextFactor() => 0.8 + (Random.Shared.NextDouble() * 0.4);
}
