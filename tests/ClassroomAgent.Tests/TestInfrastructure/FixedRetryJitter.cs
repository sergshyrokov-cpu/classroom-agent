using ClassroomAgent.Infrastructure.Google;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>A retry jitter that always answers the same factor, so a test knows each pause exactly (US-017 FR-003).</summary>
public sealed class FixedRetryJitter(double factor) : IGoogleRetryJitter
{
    public double NextFactor() => factor;
}
