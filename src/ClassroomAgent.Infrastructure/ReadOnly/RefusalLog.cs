using ClassroomAgent.Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Infrastructure.ReadOnly;

/// <summary>
/// The single refusal log event, so the guard decorator and the commit backstop decorator write the same
/// line (US-007 spec FR-009). Level <c>Warning</c> per DC-10; the exception is not passed to the logger,
/// so no stack trace reaches the file (SC-10).
/// </summary>
internal static class RefusalLog
{
    private static readonly EventId Refused = new(5201, "ReadOnlyWriteRefused");

    public static void Write(ILogger logger, ReadOnlyModeException refusal) =>
        logger.LogWarning(
            Refused,
            "Refused in read-only mode: {Operation} ({Reason})",
            refusal.Operation,
            refusal.Reason);
}
