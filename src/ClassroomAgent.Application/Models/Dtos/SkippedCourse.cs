namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>SkippedCourse</c>: T3 of one course (hint H1).</summary>
public sealed record SkippedCourse(
    SkippedCourseVariant Variant, DateTimeOffset? NotUpdatedSince, DateTimeOffset? SkippedSince);
