using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The commit backstop (US-007 spec FR-006): in read-only mode it refuses any commit that no use case
/// declared a permitted service write, so a forgotten guard call cannot write (AC-004). Defence in depth -
/// the guard is the primary point. It holds no <c>DbContext</c> and no persistence knowledge: it wraps the
/// port (AD-3, AD-6, AD-7).
/// </summary>
public sealed class ReadOnlyModeUnitOfWork(
    IUnitOfWork inner,
    GetLegitimacyModeQuery mode,
    ServiceWriteScope writeScope) : IUnitOfWork
{
    /// <summary>The operation name the backstop's own refusal carries (spec FR-006).</summary>
    public const string Operation = "UnitOfWork.SaveChanges";

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Skeleton declared at TEST_WRITING (US-007 OD-003, option 1); IMPLEMENTATION owns this file.
        _ = inner;
        _ = mode;
        _ = writeScope;
        throw new NotImplementedException();
    }
}
