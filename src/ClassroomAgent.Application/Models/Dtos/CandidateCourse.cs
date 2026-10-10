namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>A candidate course with its share, a whole percent rounded down (US-032 spec FR-003, FR-007).</summary>
public sealed record CandidateCourse(CourseOption Course, int SharePercent);
