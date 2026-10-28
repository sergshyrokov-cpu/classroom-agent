namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>AboveMaximumGrade</c>: one grade of T1.</summary>
public sealed record AboveMaximumGrade(string ItemTitle, string StudentName, decimal Points, decimal Maximum, bool IsDraft);
