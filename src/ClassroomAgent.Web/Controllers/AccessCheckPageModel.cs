using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The view model of the check-access page (US-011 openapi <c>AccessCheckPageModel</c>; AD-8). It carries no key, no
/// reference, no token and nothing a Google answer contained (spec S-07, S-08).
/// </summary>
/// <param name="ConnectionState">The US-009 connection state, read, never re-derived.</param>
/// <param name="TechnicalAccount">The stored impersonation user, or null when none is stored.</param>
/// <param name="Domain">The school's domain from <c>LegitimacyState</c>, or null while unknown.</param>
/// <param name="IsReadOnly">Whether the installation is in read-only mode.</param>
/// <param name="ReadOnlyReason">Why, when it is (BR-025).</param>
/// <param name="MessageKey">The refusal of an unusable connection, as a translation key.</param>
/// <param name="Result">The check's result after a run; null on the page itself and on a refusal.</param>
public sealed record AccessCheckPageModel(
    WorkspaceConnectionState ConnectionState,
    string? TechnicalAccount,
    string? Domain,
    bool IsReadOnly,
    LegitimacyModeReason? ReadOnlyReason,
    string? MessageKey,
    AccessCheckResult? Result);
