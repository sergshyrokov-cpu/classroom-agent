using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Stands in for a port that calls a Google API until the first real one arrives (US-009, US-011, US-013).
/// It carries the <see cref="IGoogleDataPort"/> marker, so US-007 spec FR-007 and the structural rule of
/// FR-010 apply to any use case holding it.
/// </summary>
public interface ISyntheticGooglePort : IGoogleDataPort
{
    Task ReadAsync(CancellationToken cancellationToken);
}
