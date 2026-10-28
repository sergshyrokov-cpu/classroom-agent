namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>DataHealthPageModel</c>: the «Стан даних» page, role-shaped in Application.</summary>
public sealed record DataHealthPageModel(
    bool IsAdmin,
    DataHealthMessage? Message,
    int? ConfirmedCount,
    ReadOnlyNotice? ReadOnly,
    bool HasNoProblem,
    IReadOnlyList<TechnicalProblem> Technical,
    IReadOnlyList<CourseProblems> Teaching,
    IReadOnlyList<CourseProblems> Archived);
