namespace ClassroomAgent.Application.Models;

/// <summary>US-062 entity model §2.2, db-design §5.3: one grade above its item's maximum (T1).</summary>
public sealed record AboveMaximumGradeRow(
    long CourseId, string ItemTitle, string StudentName, decimal Points, decimal Maximum, bool IsDraft);
