using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The connection as every consumer sees it (US-009 spec FR-002, FR-003): the state, the domain the Owner
/// approved and the stored values. Reading writes nothing — not even when the stored domain no longer
/// matches (spec I-5).
/// </summary>
/// <remarks>
/// The allowed domain comes from <see cref="Ports.ILegitimacyStateRepository"/>, which US-005 keeps and which
/// the legitimacy check writes even in read-only mode. It is "known" only when a check has actually succeeded:
/// an <c>upgrade_required</c> answer records a domain without moving the last successful check, and that
/// domain licenses nothing (spec FR-003, AC-010).
/// </remarks>
public sealed class GetWorkspaceConnectionQuery(
    IWorkspaceConnectionRepository connections,
    ILegitimacyStateRepository states)
{
    public async Task<WorkspaceConnectionView> ExecuteAsync(CancellationToken cancellationToken)
    {
        var domain = await KnownDomainAsync(cancellationToken);
        var connection = await connections.GetForReadAsync(cancellationToken);

        var state = (domain, connection) switch
        {
            (null, _) => WorkspaceConnectionState.DomainUnknown,
            (_, null) => WorkspaceConnectionState.NotConfigured,
            _ when DomainComparison.AreSame(connection.Domain, domain) => WorkspaceConnectionState.Configured,
            _ => WorkspaceConnectionState.DomainMismatch,
        };

        return new WorkspaceConnectionView(
            state,
            state == WorkspaceConnectionState.Configured,
            domain,
            connection?.Domain,
            connection?.ImpersonationUserEmail);
    }

    /// <summary>The <c>Installation</c> domain of the last successful check, or null while none has succeeded.</summary>
    /// <remarks>
    /// The rule itself is <see cref="ConfirmedLegitimacy"/>, shared with the super-admin instruction of US-010
    /// so that "known" cannot be decided two subtly different ways (US-010 spec I-1).
    /// </remarks>
    public async Task<string?> KnownDomainAsync(CancellationToken cancellationToken) =>
        ConfirmedLegitimacy.DomainOf(await states.GetForReadAsync(cancellationToken));
}
