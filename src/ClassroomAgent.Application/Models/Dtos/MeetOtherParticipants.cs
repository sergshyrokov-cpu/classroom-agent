namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Report 4's one "Other participants" line: count and total capped seconds, no identity (spec I-11).</summary>
public sealed record MeetOtherParticipants(int Count, long TotalSeconds);
