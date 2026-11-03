namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-071 entity model §3: a course of a found student; <see cref="Left"/> is the BR-068 mark, null on the roster.</summary>
public sealed record StudentSearchCourse(long CourseId, string Name, string? Section, LeftMark? Left);
