namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// A use case holding a port marked <c>IGoogleDataPort</c> without taking the guard - the Google-path
/// violation the structural rule must flag (US-007 spec FR-007, FR-010). It exists only to prove the rule
/// detects, and is never registered in the host.
/// </summary>
public sealed class UnguardedGoogleUseCase(ISyntheticGooglePort google)
{
    public Task ExecuteAsync(CancellationToken cancellationToken) => google.ReadAsync(cancellationToken);
}
