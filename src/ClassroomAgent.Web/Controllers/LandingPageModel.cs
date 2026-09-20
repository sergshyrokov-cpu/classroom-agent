using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// View DTO of the landing page (US-008 openapi <c>LandingPageModel</c>; AD-8): who is signed in, their role, and
/// the installation's legitimacy status. No domain entity and no teaching data — none exists yet.
/// </summary>
/// <remarks>
/// <see cref="Email"/> is data from Google, rendered as it came and never translated (spec FR-017).
/// <see cref="LastSuccessfulCheckAt"/> is UTC: the school time zone is not a setting yet (spec I-12).
/// </remarks>
public sealed record LandingPageModel(
    string Email,
    string RoleKey,
    bool IsReadOnly,
    LegitimacyModeReason? ReadOnlyReason,
    DateTimeOffset? LastSuccessfulCheckAt);
