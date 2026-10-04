using System.Security.Claims;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Security;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// Builds the view models of the two pages that carry the "Synchronize" button (US-019 api-design §2.4): the page a
/// refused press re-renders must be the same page its own <c>GET</c> renders, plus the message, so both controllers
/// build it here. Reads only; no business rule (AD-3).
/// </summary>
public sealed class InstallationPages(
    GetWorkspaceConnectionQuery connectionQuery,
    GetLegitimacyModeQuery legitimacyMode,
    GetLastSynchronizationQuery lastSynchronization)
{
    /// <summary>The connection settings page (US-009, US-017; US-019 adds the button and its message).</summary>
    public async Task<WorkspaceConnectionPageModel> WorkspaceConnectionAsync(
        string typed,
        string? messageKey,
        IReadOnlyList<string> fieldErrorKeys,
        string? synchronizationMessageKey,
        CancellationToken cancellationToken)
    {
        var view = await connectionQuery.ExecuteAsync(cancellationToken);
        var mode = await legitimacyMode.ExecuteAsync(cancellationToken);
        var last = await lastSynchronization.ExecuteAsync(cancellationToken);
        return new WorkspaceConnectionPageModel(
            view.State,
            view.InstallationDomain,
            view.SavedImpersonationUserEmail,
            typed,
            mode.IsReadOnly,
            mode.Reason,
            messageKey,
            fieldErrorKeys,
            last,
            synchronizationMessageKey);
    }

    /// <summary>
    /// The landing page (US-008). The button is a Dean's only (US-019 spec I-6); nothing about any run is added
    /// (spec S-08).
    /// </summary>
    public async Task<LandingPageModel> LandingAsync(
        ClaimsPrincipal user,
        string? synchronizationMessageKey,
        CancellationToken cancellationToken)
    {
        var mode = await legitimacyMode.ExecuteAsync(cancellationToken);
        var isDean = user.IsInRole(InstallationSession.RoleName(AppRole.Dean));
        return new LandingPageModel(
            user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            isDean ? "Landing.Role.Dean" : "Landing.Role.Admin",
            mode.IsReadOnly,
            mode.Reason,
            mode.LastSuccessfulCheckAt,
            CanRequestSynchronization: isDean,
            SynchronizationMessageKey: synchronizationMessageKey);
    }
}
