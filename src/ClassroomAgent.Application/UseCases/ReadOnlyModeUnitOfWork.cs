using ClassroomAgent.Application.Exceptions;
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // A declared write of the BR-026 closed list commits whatever the mode is (AC-003).
        if (writeScope.Current is null)
        {
            var current = await mode.ExecuteAsync(cancellationToken);
            if (current is { IsReadOnly: true, Reason: { } reason })
            {
                throw new ReadOnlyModeException(reason, current.LastSuccessfulCheckAt, Operation);
            }
        }

        await inner.SaveChangesAsync(cancellationToken);
    }
}
