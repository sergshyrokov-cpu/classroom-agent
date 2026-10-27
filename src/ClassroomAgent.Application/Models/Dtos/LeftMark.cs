namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// US-060 spec FR-006, VR-005: a student off the course's roster. <see cref="LastSeenOn"/> is null for a student known
/// only through submissions (OD-003 a).
/// </summary>
public sealed record LeftMark(DateOnly? LastSeenOn);
