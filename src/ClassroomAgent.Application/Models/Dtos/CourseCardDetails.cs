using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-021 <c>CourseCardDetails</c> (spec FR-002); times are school-local, to the minute.</summary>
public sealed record CourseCardDetails(
    long CourseId,
    string Name,
    string? Section,
    CourseState State,
    string? Room,
    string? DescriptionHeading,
    string? Description,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);
