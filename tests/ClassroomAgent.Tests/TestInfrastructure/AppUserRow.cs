namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>A stored installation <c>app_user</c> row, read with raw SQL (US-008 db-design 3).</summary>
public sealed record AppUserRow(
    long Id,
    string Email,
    string NormalizedEmail,
    string Role,
    string SignInMethod,
    string? PasswordHash,
    int AccessFailedCount,
    DateTimeOffset? LockoutEnd,
    string UiLanguage,
    bool IsDisabled,
    DateTimeOffset? LastSuccessfulSignInAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
