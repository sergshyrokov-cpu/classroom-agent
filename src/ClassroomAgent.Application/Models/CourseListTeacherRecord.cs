namespace ClassroomAgent.Application.Models;

/// <summary>A current teacher of one course of the page (US-020 entity model §2, Q5).</summary>
public sealed record CourseListTeacherRecord(long CourseId, CourseListPersonRecord Teacher);
