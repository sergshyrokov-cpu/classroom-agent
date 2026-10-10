namespace ClassroomAgent.Application.MeetLinking;

/// <summary>A code's candidates, best share first, then course id (US-032 spec FR-003); empty = no candidates.</summary>
public sealed record MeetCodeScore(string MeetingCode, IReadOnlyList<CandidateShare> Candidates);
