namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-071 entity model §3: a found teacher.</summary>
public sealed record TeacherSearchRow(long ParticipantId, string Label, string? Email);
