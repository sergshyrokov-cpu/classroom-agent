using ClassroomAgent.ControlPlane.Services;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// View model of the installation detail page: the US-002/US-003 DTO plus the validated
/// "unchanged" notice key of US-004 (api-design §4), null when no notice is shown.
/// </summary>
public sealed record InstallationDetailPageModel(InstallationDetailDto Installation, string? NoticeKey);
