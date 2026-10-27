using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// One item dated in the period — a journal column (US-025 entity model §3, db-design Q2); <see cref="NotInClassroom"/>
/// is US-060 spec FR-005 (BR-045).
/// </summary>
public sealed record JournalItemRecord(
    long Id,
    string Title,
    DateTimeOffset ItemDate,
    DateTimeOffset? DueAt,
    decimal? MaxPoints,
    CourseWorkKind Kind,
    bool NotInClassroom = false);
