namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-024 spec FR-002: one count per course state and their sum.</summary>
public sealed record CourseCounts(int Active, int Archived, int Provisioned, int Declined, int Suspended, int Total);
