namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>A stored <c>installation</c> row, read with raw SQL (US-002 db-design §3).</summary>
public sealed record InstallationRow(
    long Id,
    Guid Identifier,
    string Name,
    string Domain,
    string ClientId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
