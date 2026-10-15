namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-038 <c>ErasureCourseEntry</c>: the course name as stored, never translated, and the role.</summary>
public sealed record ErasureCourseEntry(string CourseName, ErasureCourseRole Role);
