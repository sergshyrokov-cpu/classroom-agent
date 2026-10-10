using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The Meet audit log of Admin Reports, read as the school's technical account (US-031 spec FR-003, FR-004; AD-4,
/// BR-015). Returns the <c>call_ended</c> events of a window page by page, mapped to application values only; no Google
/// SDK type crosses this port. A final failure leaves it as <see cref="GoogleReadFailedException"/> (US-017).
/// </summary>
public interface IMeetReportsReader : IGoogleDataPort
{
    /// <summary>The <c>call_ended</c> events in [<paramref name="from"/>, <paramref name="to"/>), page by page.</summary>
    IAsyncEnumerable<MeetEventPage> ReadCallEndedAsync(
        string impersonationUser,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
