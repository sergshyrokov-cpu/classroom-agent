namespace ClassroomAgent.Application.Models;

/// <summary>
/// One row of the Admin's Dean accounts screen — exactly the columns OD-003 fixed (US-012 spec FR-011;
/// entity model §2). A DTO built in <c>Application</c>, never an entity (AD-8), and it carries no password
/// hash, no security stamp and no lockout state (spec S-10).
/// </summary>
public sealed record DeanAccountRow(
    long Id,
    string Email,
    bool IsDisabled,
    bool PasswordIsTemporary,
    DateTimeOffset? LastSuccessfulSignInAt);
