namespace ClassroomAgent.Application.Models;

/// <summary>
/// What the per-request session check needs from an account and nothing more (US-008 security review F-4): the
/// security stamp the cookie is compared against, and whether the account is disabled. Read untracked, so the
/// check cannot leave a tracked entity behind for a later commit.
/// </summary>
public sealed record AccountSessionState(string SecurityStamp, bool IsDisabled);
