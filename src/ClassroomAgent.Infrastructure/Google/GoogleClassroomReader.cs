using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The Google implementation of <see cref="IClassroomReader"/> (US-014 spec FR-002; entity model §6): the only
/// place the Google SDK types for Classroom courses and rosters exist (AD-4). Every request goes through the
/// injected transport, so nothing but Google is reachable from here (SC-13) and the tests drive it offline
/// (TC-4). The client library's own retries are switched off (retry is US-017, OD-008).
/// </summary>
/// <remarks>
/// <see cref="ReadCoursesAsync"/> reports a course's state as the string Google sent, so the use case — not this
/// adapter — applies OD-010 and skips a value it does not recognise.
/// <para>
/// Compile-only skeleton created at TEST_WRITING under US-014 OD-012; IMPLEMENTATION owns it from here.
/// </para>
/// </remarks>
public sealed class GoogleClassroomReader : IClassroomReader
{
    public GoogleClassroomReader(
        ISecretStore secretStore,
        GoogleServiceAccountSettings settings,
        HttpMessageHandler transport,
        ILogger<GoogleClassroomReader>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(transport);

        // secretStore, settings, transport and logger are not yet stored: nothing in this compile-only
        // skeleton reads them. IMPLEMENTATION (US-014 OD-012) is what wires them into the two members below.
    }

    public IAsyncEnumerable<CourseSnapshot> ReadCoursesAsync(
        string impersonationUser,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<CourseRoster> ReadRosterAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
