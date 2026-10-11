using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>US-021 entity model §5: <see cref="Page"/> is non-null exactly when <see cref="Outcome"/> is Shown.</summary>
public sealed record CourseCardResult(CourseCardOutcome Outcome, CourseCardPageModel? Page);
