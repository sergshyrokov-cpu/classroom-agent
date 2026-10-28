namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>CourseLabel</c>: a loaded course by id and name, a never-loaded one by its skip record (FR-001.6).</summary>
public sealed record CourseLabel(long? CourseId, string? Name, string? GoogleCourseId);
