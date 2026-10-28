namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>TechnicalDetails</c>: the Admin's details of a technical problem.</summary>
public sealed record TechnicalDetails(
    string? Diagnosis,
    string? FailedStep,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? LastSuccessAt,
    bool? UpgradeRequired,
    CourseLabel? Course,
    DateTimeOffset? SkippedSince,
    IReadOnlyList<NotVisibleCourse>? NotVisibleCourses);
