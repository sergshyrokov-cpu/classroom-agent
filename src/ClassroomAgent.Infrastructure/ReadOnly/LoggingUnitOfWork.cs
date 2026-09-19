using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Ports;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Infrastructure.ReadOnly;

/// <summary>
/// The same line for a refusal raised by the commit backstop (US-007 spec FR-006, FR-009): a write whose
/// author forgot the guard is refused and logged exactly like a guarded one (AC-004, AC-010).
/// </summary>
public sealed class LoggingUnitOfWork(IUnitOfWork inner, ILogger<LoggingUnitOfWork> logger) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await inner.SaveChangesAsync(cancellationToken);
        }
        catch (ReadOnlyModeException refusal)
        {
            RefusalLog.Write(logger, refusal);
            throw;
        }
    }
}
