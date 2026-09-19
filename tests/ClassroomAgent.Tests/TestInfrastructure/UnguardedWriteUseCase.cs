using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The violation US-007 AC-004 is about: a write path whose author forgot the guard and which declares no
/// permitted service write. In read-only mode the commit backstop must refuse it anyway (spec FR-006), and
/// the structural rule must flag it (spec FR-010).
/// </summary>
public sealed class UnguardedWriteUseCase(ILegitimacyStateRepository states, IUnitOfWork unitOfWork)
{
    public Task ExecuteAsync(DateTimeOffset checkedAt, CancellationToken cancellationToken) =>
        SyntheticWriteUseCase.WriteAsync(states, unitOfWork, checkedAt, cancellationToken);
}
