namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Report 3 (spec FR-009).</summary>
public sealed record StudentParticipationRow(PersonRef Student, int Meetings, long TotalSeconds);
