namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>The stored <c>owner</c> row, read with raw SQL (db-design §3).</summary>
public sealed record OwnerRow(
    long Id,
    string UserName,
    string NormalizedUserName,
    string PasswordHash,
    string SecurityStamp,
    int AccessFailedCount,
    DateTimeOffset? LockoutEnd,
    string UiLanguage);
