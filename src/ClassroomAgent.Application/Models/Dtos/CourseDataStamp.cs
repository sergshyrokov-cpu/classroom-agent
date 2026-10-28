namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// US-063 spec FR-001, FR-002: the instant B the journal or report was built for, and the course's last complete read
/// (null when none is recorded, FR-002.3) — both already converted to the school's zone (FR-006.1).
/// </summary>
public sealed record CourseDataStamp(DateTimeOffset GeneratedAt, DateTimeOffset? DataAsOf);
