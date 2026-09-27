using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Models;

/// <summary>
/// The view model of the Admin's Dean accounts screen (US-012 openapi <c>DeanAccountsPageModel</c>). A DTO built
/// in the presentation layer from Application models, never an entity (AD-8); it carries no password, no hash
/// and no security stamp (spec S-10).
/// </summary>
public sealed record DeanAccountsPageModel(
    IReadOnlyList<DeanAccountRow> Deans,
    bool IsReadOnly,
    LegitimacyModeReason? ReadOnlyReason,
    string? EmailInput,
    string? MessageKey);
