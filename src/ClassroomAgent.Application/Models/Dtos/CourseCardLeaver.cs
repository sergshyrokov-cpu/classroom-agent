using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-021 <c>CourseCardLeaver</c>: a membership off the roster (spec FR-003, FR-006).</summary>
public sealed record CourseCardLeaver(
    long ParticipantId, CourseListPersonName Name, string? Email, ClassroomRole Role, DateOnly LastSeenOn);
