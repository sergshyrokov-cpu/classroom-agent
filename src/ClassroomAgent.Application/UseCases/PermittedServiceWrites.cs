namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Which use case performs which write of the <see cref="PermittedServiceWrite"/> closed list (US-007 spec
/// FR-005). The structural test of AC-007 reads this registry: a write path that is neither guarded nor
/// registered here fails the suite. An entry is added only for a write already on the BR-026 list - and
/// that list grows only by being extended in <c>trebovaniya.md</c> §2 first.
/// </summary>
public static class PermittedServiceWrites
{
    /// <summary>Use-case type to the BR-026 write it performs.</summary>
    public static IReadOnlyDictionary<Type, PermittedServiceWrite> Declarations { get; } =
        new Dictionary<Type, PermittedServiceWrite>
        {
            // US-005: without it the installation could never leave read-only mode (AC-003, AC-009).
            [typeof(CheckLegitimacyUseCase)] = PermittedServiceWrite.LegitimacyCheckState,
        };
}
