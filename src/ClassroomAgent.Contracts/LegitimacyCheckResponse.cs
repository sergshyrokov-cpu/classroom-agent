namespace ClassroomAgent.Contracts;

/// <summary>Legitimacy check answer for a known installation (US-005 api-design §4). Wire type only.</summary>
public sealed record LegitimacyCheckResponse(string Status, string Compatibility, string Domain, string ClientId);
