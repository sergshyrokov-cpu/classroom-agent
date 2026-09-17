using ClassroomAgent.ControlPlane.Persistence;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>One row of the installations list (US-002 api-design §6). No internal key, no secret.</summary>
public sealed record InstallationListItemDto(
    Guid Identifier,
    string Name,
    string Domain,
    InstallationStatus Status,
    DateTimeOffset CreatedAt);
