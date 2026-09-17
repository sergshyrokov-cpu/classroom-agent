using ClassroomAgent.ControlPlane.Persistence;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>The installation detail page (US-002 api-design §6): the list values plus the client ID.</summary>
public sealed record InstallationDetailDto(
    Guid Identifier,
    string Name,
    string Domain,
    InstallationStatus Status,
    DateTimeOffset CreatedAt,
    string ClientId);
