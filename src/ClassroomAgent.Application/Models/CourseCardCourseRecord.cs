using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>US-021 Q1 — no Classroom link, teacher folder, calendar id, Google id or owner id (spec FR-002, S-10);
/// the mark and its instant are US-060 spec FR-001.</summary>
public sealed record CourseCardCourseRecord(
    long CourseId,
    string Name,
    string? Section,
    CourseState State,
    string? Room,
    string? DescriptionHeading,
    string? Description,
    DateTimeOffset? CreationTime,
    DateTimeOffset? UpdateTime,
    CourseAbsenceMark? AbsenceMark = null,
    DateTimeOffset? AbsentSince = null);
