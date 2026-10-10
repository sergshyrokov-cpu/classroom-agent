namespace ClassroomAgent.Application.MeetLinking;

/// <summary>Whether a code is unambiguous and, if so, its course (US-032 spec FR-004).</summary>
public sealed record MeetCodeDecision(bool Unambiguous, long? CourseId);
