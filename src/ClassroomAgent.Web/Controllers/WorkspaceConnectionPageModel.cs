using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// View DTO of the connection settings (US-009 openapi <c>WorkspaceConnectionPageModel</c>; AD-8). Every
/// message is a translation <b>key</b>, never a rendered sentence: <c>Application</c> holds no user-visible
/// string (AD-6, NFR-073). The domain and the address are data and are rendered as stored.
/// </summary>
/// <param name="State">The connection state (spec FR-002).</param>
/// <param name="InstallationDomain">The domain from <c>LegitimacyState</c>, or null while none is known.</param>
/// <param name="SavedImpersonationUserEmail">The stored technical account, or null when none is stored.</param>
/// <param name="TypedImpersonationUserEmail">What the Admin typed, kept after a refusal so it can be corrected.</param>
/// <param name="IsReadOnly">Whether the installation is in read-only mode (US-007).</param>
/// <param name="ReadOnlyReason">Why, when it is (BR-025).</param>
/// <param name="MessageKey">The confirmation of a save, or the refusal of a `409`.</param>
/// <param name="FieldErrorKeys">Per-field validation messages of a `400`, by field name.</param>
/// <param name="LastSynchronization">The "Last synchronization" block (US-017 spec FR-007), shown on every rendering.</param>
/// <param name="SynchronizationMessageKey">
/// US-019 spec FR-003, FR-004: the one-time message after a press, or the refusal of its <c>409</c>; null otherwise.
/// </param>
public sealed record WorkspaceConnectionPageModel(
    WorkspaceConnectionState State,
    string? InstallationDomain,
    string? SavedImpersonationUserEmail,
    string TypedImpersonationUserEmail,
    bool IsReadOnly,
    LegitimacyModeReason? ReadOnlyReason,
    string? MessageKey,
    IReadOnlyList<string> FieldErrorKeys,
    LastSynchronizationView LastSynchronization,
    string? SynchronizationMessageKey = null)
{
    /// <summary>US-019 OD-006: the button is always rendered for the Admin here, read-only mode included (AD-6).</summary>
    public bool CanRequestSynchronization => true;
}
