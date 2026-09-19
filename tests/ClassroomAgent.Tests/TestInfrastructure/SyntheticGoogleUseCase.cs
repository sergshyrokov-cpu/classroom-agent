using ClassroomAgent.Application.UseCases;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// A use case that would reach Google: it calls the guard first, so in read-only mode the port is never
/// touched (US-007 spec FR-007, AC-006). "Check access" and the startup access self-check will have this
/// shape (SC-5, trebovaniya.md §2).
/// </summary>
public sealed class SyntheticGoogleUseCase(IReadOnlyModeGuard guard, ISyntheticGooglePort google)
{
    public const string Operation = "SyntheticGoogleRead";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await guard.EnsureAllowedAsync(Operation, cancellationToken);
        await google.ReadAsync(cancellationToken);
    }
}
