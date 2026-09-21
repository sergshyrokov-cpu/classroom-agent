using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// View DTO of the super-admin instruction (US-010 openapi <c>ConnectionInstructionPageModel</c>; AD-8): no
/// domain entity, no secret, and none of the two identifiers §6 (v78) warns against confusing with the client
/// ID (spec FR-006).
/// </summary>
/// <remarks>
/// <see cref="ReadOnly"/> does not gate the page: BR-026 names the instruction as viewable, so read-only mode is
/// reported alongside a complete instruction rather than instead of it (api-design §2.3, §2.4).
/// </remarks>
/// <param name="State">Complete, or not yet confirmed (spec FR-002).</param>
/// <param name="InstallationDomain">Data from Google's side of the fence: rendered as stored, never translated.</param>
/// <param name="ServiceAccountClientId">Likewise, and rendered with no formatting so it can be pasted into the Google console (spec VR-004).</param>
/// <param name="Scopes">The six scopes of <c>trebovaniya.md</c> §6.</param>
/// <param name="ReadOnly">Whether the installation is in read-only mode (US-007).</param>
/// <param name="ReadOnlyReason">The BR-025 cause, as an enum the view turns into a key — never a rendered sentence (AD-6).</param>
public sealed record ConnectionInstructionPageModel(
    ConnectionInstructionState State,
    string? InstallationDomain,
    string? ServiceAccountClientId,
    IReadOnlyList<string> Scopes,
    bool ReadOnly,
    LegitimacyModeReason? ReadOnlyReason);
