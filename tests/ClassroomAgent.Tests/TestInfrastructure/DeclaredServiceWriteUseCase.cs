using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// A write that declares itself one of the BR-026 closed list and therefore commits in read-only mode
/// without calling the guard (US-007 spec FR-005, AC-003) - the shape <c>CheckLegitimacyUseCase</c> has.
/// It stands in for the three members of the list that have no use case yet (US-008, US-012, EPIC-10).
/// </summary>
public sealed class DeclaredServiceWriteUseCase(
    ILegitimacyStateRepository states,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope)
{
    public async Task ExecuteAsync(DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        using var declaration = writeScope.Declare(PermittedServiceWrite.LegitimacyCheckState);
        await SyntheticWriteUseCase.WriteAsync(states, unitOfWork, checkedAt, cancellationToken);
    }
}
