using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// One stored course for the journal drop-down (US-025 entity model §3, db-design Q1), with what US-060 spec FR-003
/// reads for its header line: the stored mark, the last complete read and whether a skip record names the course.
/// </summary>
public sealed record JournalCourseRecord(
    long Id,
    string Name,
    string? Section,
    CourseAbsenceMark? AbsenceMark = null,
    DateTimeOffset? AbsentSince = null,
    DateTimeOffset? LastCompleteReadAt = null,
    bool Skipped = false);
