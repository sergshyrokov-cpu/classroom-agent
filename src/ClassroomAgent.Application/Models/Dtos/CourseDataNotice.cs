namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// US-060 spec FR-003, VR-002: the one header line of a course. <see cref="Since"/> is null only for
/// <see cref="CourseDataNoticeKind.NotUpdated"/> without a last complete read (OD-002 a).
/// </summary>
public sealed record CourseDataNotice(CourseDataNoticeKind Kind, DateOnly? Since);
