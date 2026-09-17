namespace ClassroomAgent.Application.Models;

/// <summary>Whether the installation is in read-only mode, why, and since which successful check (US-005 spec FR-008).</summary>
public sealed record LegitimacyMode(bool IsReadOnly, LegitimacyModeReason? Reason, DateTimeOffset? LastSuccessfulCheckAt);
