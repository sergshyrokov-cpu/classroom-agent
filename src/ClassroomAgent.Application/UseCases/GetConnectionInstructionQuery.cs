using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The super-admin instruction as the screen sees it (US-010 spec FR-001, FR-002, FR-003): this school's own
/// service-account client ID and domain from <c>LegitimacyState</c>, plus the six scopes fixed in
/// <c>trebovaniya.md</c> §6.
/// </summary>
/// <remarks>
/// Reading writes nothing — no audit row, no table, in any mode (spec FR-010, AC-007): the state is read
/// untracked, so nothing is ever staged and the unit of work is never asked to commit.
///
/// The values are read on **every** request and cached nowhere, which is what makes a rotated client ID appear
/// with no restart and no action on the school's server (spec AC-006). The Control Plane is not asked here: an
/// installation whose Control Plane is unreachable must still be able to show its instruction (BR-026).
///
/// "Known" is <see cref="ConfirmedLegitimacy"/>, the same rule the connection settings use (spec I-1).
/// </remarks>
public sealed class GetConnectionInstructionQuery(ILegitimacyStateRepository states)
{
    public async Task<ConnectionInstructionView> ExecuteAsync(CancellationToken cancellationToken)
    {
        var state = await states.GetForReadAsync(cancellationToken);
        var domain = ConfirmedLegitimacy.DomainOf(state);
        var clientId = ConfirmedLegitimacy.ClientIdOf(state);

        // Both values, or neither: a half-known installation is reported as not confirmed, so no instruction
        // ever names a school without naming the client ID it must authorise (spec FR-002).
        return domain is null || clientId is null
            ? new ConnectionInstructionView(
                ConnectionInstructionState.InstallationNotConfirmed,
                null,
                null,
                GoogleDelegationScopes.All)
            : new ConnectionInstructionView(
                ConnectionInstructionState.Complete,
                domain,
                clientId,
                GoogleDelegationScopes.All);
    }
}
