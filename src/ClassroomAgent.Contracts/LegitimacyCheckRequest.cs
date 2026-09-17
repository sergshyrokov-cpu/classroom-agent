namespace ClassroomAgent.Contracts;

/// <summary>
/// Legitimacy check request (US-005 api-design §4). Wire type only: the members are nullable so a
/// missing property reaches validation instead of a default value.
/// </summary>
public sealed record LegitimacyCheckRequest(Guid? InstallationId, string? ApplicationVersion, int? ContractVersion);
