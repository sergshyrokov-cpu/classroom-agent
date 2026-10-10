namespace ClassroomAgent.Application.Models;

/// <summary>What the linking step did in one run, for the log only (US-032 spec FR-006, §9).</summary>
public sealed record MeetLinkingCounts(int CodesScored, int LinksCreated);
