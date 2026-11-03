using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-071 entity model §3: a found course; the view words <see cref="State"/> as the course list's row does.</summary>
public sealed record CourseSearchRow(long CourseId, string Name, string? Section, CourseState State);
