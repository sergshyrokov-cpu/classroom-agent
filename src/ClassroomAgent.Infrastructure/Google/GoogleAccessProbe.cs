using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The Google implementation of <see cref="IGoogleAccessProbe"/> (US-011 spec FR-004, FR-005). The only code that
/// references the Google packages; every answer is mapped onto the closed list of spec FR-005.
/// </summary>
/// <remarks>
/// Compile-only skeleton created at TEST_WRITING under US-011 OD-006. <paramref name="transport"/> carries every
/// request to Google, so the tests drive it offline (TC-4). IMPLEMENTATION owns this file from here.
/// </remarks>
public sealed class GoogleAccessProbe : IGoogleAccessProbe
{
    public GoogleAccessProbe(
        ISecretStore secretStore,
        GoogleServiceAccountSettings settings,
        HttpMessageHandler transport)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(transport);
    }

    public Task<DelegationAttempt> RequestDelegatedTokenAsync(
        string technicalAccount,
        string scope,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<AccessCheckStepOutcome> ReadCoursesAsync(DelegatedToken token, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<AccessCheckStepOutcome> ReadMeetActivityAsync(DelegatedToken token, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
