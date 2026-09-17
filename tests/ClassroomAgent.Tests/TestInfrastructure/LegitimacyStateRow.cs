namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>One <c>legitimacy_state</c> row as stored (US-005 db-design §4).</summary>
public sealed record LegitimacyStateRow(
    long Id,
    bool Singleton,
    DateTimeOffset? LastSuccessfulCheckAt,
    string Status,
    string Compatibility,
    string Domain,
    string ClientId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
