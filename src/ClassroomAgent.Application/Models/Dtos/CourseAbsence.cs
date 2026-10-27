using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// US-060 spec FR-001: the stored course mark of BR-048 / BR-049 shown next to the status in the course list and on
/// the card; <see cref="Since"/> is the mark's instant as a date of the school's zone (BR-053).
/// </summary>
public sealed record CourseAbsence(CourseAbsenceMark Mark, DateOnly Since);
