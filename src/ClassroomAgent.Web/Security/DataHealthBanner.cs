using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// US-062 api-design §2.3, spec FR-003: what the installation layout asks for its banner. The counts come only from
/// <see cref="GetDataHealthCountsQuery"/> (no counting in Web, AD-3); null — nothing rendered — without a signed-in
/// Admin or Dean, on the forced password change (OD-001 a), with no counted problem, or when the computation failed
/// (spec I-4: the page renders without the banner and the failure is logged by exception type only, SC-10).
/// </summary>
public sealed class DataHealthBanner(GetDataHealthCountsQuery counts, ILogger<DataHealthBanner> logger)
{
    public async Task<DataHealthBannerModel?> ForAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.User.Identity?.IsAuthenticated != true
            || context.Request.Path.StartsWithSegments(SignInRoutes.ForcedPasswordChange, StringComparison.OrdinalIgnoreCase)
            || RoleOf(context) is not { } role)
        {
            return null;
        }

        try
        {
            return await counts.ExecuteAsync(role, context.RequestAborted);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            DataHealthLog.BannerFailed(logger, failure.GetType().Name);
            return null;
        }
    }

    private static AppRole? RoleOf(HttpContext context) =>
        context.User.IsInRole(InstallationSession.RoleName(AppRole.Admin)) ? AppRole.Admin
        : context.User.IsInRole(InstallationSession.RoleName(AppRole.Dean)) ? AppRole.Dean
        : null;
}
