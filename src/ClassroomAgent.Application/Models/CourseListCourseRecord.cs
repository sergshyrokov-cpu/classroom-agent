using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>One course of the list as stored (US-020 entity model §2); no Classroom link (OD-008).</summary>
public sealed record CourseListCourseRecord(
    long CourseId, string Name, string? Section, CourseState State, DateTimeOffset? UpdateTime);
