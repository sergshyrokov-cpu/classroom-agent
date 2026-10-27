using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-020 <c>CourseListRow</c> (spec FR-003): <see cref="UpdatedAt"/> is <c>Course.UpdateTime</c> in the
/// school's time zone, to the minute, without an offset; null when Google gave none (spec I-8). US-060: the course mark
/// (spec FR-001) and "no teacher in Classroom" when no teacher is on the roster (spec FR-002, BR-069).
/// </summary>
public sealed record CourseListRow(
    long CourseId,
    string Name,
    string? Section,
    CourseState State,
    IReadOnlyList<CourseListPersonName> Teachers,
    int StudentCount,
    DateTime? UpdatedAt,
    CourseAbsence? Absence,
    bool NoTeacherInClassroom);
