using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// What the connection settings screen shows (US-009 openapi <c>WorkspaceConnectionPageModel</c>; AD-8): a
/// DTO, never the entity. It carries no key, no secret and no reference to either (SC-7, PC-9).
/// </summary>
/// <param name="State">The connection state (spec FR-002).</param>
/// <param name="IsUsable">True only for <see cref="WorkspaceConnectionState.Configured"/>, so a later caller cannot forget a case (OD-002).</param>
/// <param name="InstallationDomain">The domain from <c>LegitimacyState</c>, or null while none is known.</param>
/// <param name="SavedDomain">The domain the stored connection is bound to, or null when none is stored.</param>
/// <param name="SavedImpersonationUserEmail">The stored technical account, or null when none is stored.</param>
public sealed record WorkspaceConnectionView(
    WorkspaceConnectionState State,
    bool IsUsable,
    string? InstallationDomain,
    string? SavedDomain,
    string? SavedImpersonationUserEmail);
