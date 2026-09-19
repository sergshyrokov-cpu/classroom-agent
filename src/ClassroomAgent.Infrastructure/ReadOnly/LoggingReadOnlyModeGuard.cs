using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.UseCases;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Infrastructure.ReadOnly;

/// <summary>
/// Writes the one <c>Warning</c> line a refused write produces (US-007 spec FR-009; AC-010, DC-10) and
/// rethrows unchanged. It observes the refusal; the decision stays in <c>Application</c> (AC-011, OD-002
/// option 3). The line carries the operation constant and the reason category only - no personal data, no
/// payload, no stack trace (SC-10).
/// </summary>
public sealed class LoggingReadOnlyModeGuard(IReadOnlyModeGuard inner, ILogger<LoggingReadOnlyModeGuard> logger)
    : IReadOnlyModeGuard
{
    public async Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken)
    {
        try
        {
            await inner.EnsureAllowedAsync(operation, cancellationToken);
        }
        catch (ReadOnlyModeException refusal)
        {
            RefusalLog.Write(logger, refusal);
            throw;
        }
    }
}
