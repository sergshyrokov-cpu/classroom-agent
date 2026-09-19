namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Which use case performs which write of the <see cref="PermittedServiceWrite"/> closed list (US-007 spec
/// FR-005). The structural test of AC-007 reads this registry: a write path that is neither guarded nor
/// registered here fails the suite.
/// </summary>
public static class PermittedServiceWrites
{
    /// <summary>Use-case type to the BR-026 write it performs.</summary>
    public static IReadOnlyDictionary<Type, PermittedServiceWrite> Declarations =>
        throw new NotImplementedException();
}
