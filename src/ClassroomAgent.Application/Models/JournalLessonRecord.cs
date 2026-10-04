using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>One item whose lesson date lies in the period (US-027 db-design F2; entity model §4).</summary>
public sealed record JournalLessonRecord(
    long Id,
    CourseWorkResource Resource,
    string Title,
    decimal? MaxPoints,
    DateTimeOffset? DueAt,
    DateTimeOffset LessonDate);
