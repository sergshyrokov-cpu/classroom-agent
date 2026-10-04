namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One entry of the course drop-down (US-025 openapi <c>CourseOption</c>).</summary>
public sealed record CourseOption(long Id, string Name, string? Section);
