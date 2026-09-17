namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>One <c>allowed_admin</c> row as stored (US-003 db-design §3).</summary>
public sealed record AllowedAdminRow(
    long Id,
    Guid Identifier,
    long InstallationId,
    string Email,
    long AddedByOwnerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
