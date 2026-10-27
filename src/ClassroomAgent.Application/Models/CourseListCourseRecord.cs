using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// One course of the list as stored (US-020 entity model §2); no Classroom link (OD-008). The mark and its instant are
/// US-060 spec FR-001 (BR-048, BR-049).
/// </summary>
public sealed record CourseListCourseRecord(
    long CourseId,
    string Name,
    string? Section,
    CourseState State,
    DateTimeOffset? UpdateTime,
    CourseAbsenceMark? AbsenceMark = null,
    DateTimeOffset? AbsentSince = null);
