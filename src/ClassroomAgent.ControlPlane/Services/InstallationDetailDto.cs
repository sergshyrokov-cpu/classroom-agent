using ClassroomAgent.ControlPlane.Persistence;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// The installation detail page (US-002 api-design §6): the list values plus the client ID, and
/// its AllowedAdmin entries in display order (US-003 api-design §6), and its last legitimacy check, null
/// when the installation never called (US-005 api-design §9).
/// </summary>
public sealed record InstallationDetailDto(
    Guid Identifier,
    string Name,
    string Domain,
    InstallationStatus Status,
    DateTimeOffset CreatedAt,
    string ClientId,
    IReadOnlyList<AllowedAdminItemDto> Admins,
    InstallationLastCheckDto? LastCheck);
