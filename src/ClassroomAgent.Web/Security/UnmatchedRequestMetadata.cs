namespace ClassroomAgent.Web.Security;

/// <summary>
/// Marks the catch-all fallback: it answers an address no page serves, so a log line written while it is selected
/// carries the "no route" marker, not its <c>{*path}</c> pattern (US-045 spec FR-003, I-2).
/// </summary>
public sealed class UnmatchedRequestMetadata
{
}
