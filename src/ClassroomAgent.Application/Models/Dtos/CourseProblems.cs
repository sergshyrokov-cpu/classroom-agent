namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>CourseProblems</c>: one course and its per-course problems.</summary>
public sealed record CourseProblems(
    CourseLabel Course,
    string? JournalPath,
    DateTimeOffset? NotInClassroomSince,
    SkippedCourse? Skipped,
    IReadOnlyList<ItemNotInClassroom> ItemsNotInClassroom,
    IReadOnlyList<AboveMaximumGrade> AboveMaximum,
    bool? UnknownState);
