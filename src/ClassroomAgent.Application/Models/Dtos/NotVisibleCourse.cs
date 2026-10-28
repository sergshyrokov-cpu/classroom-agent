namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>NotVisibleCourse</c>: one course of X5 — the confirmation form's checkbox.</summary>
public sealed record NotVisibleCourse(
    long CourseId, string Name, string State, DateTimeOffset MarkedSince, DateTimeOffset? LastCompleteReadAt);
