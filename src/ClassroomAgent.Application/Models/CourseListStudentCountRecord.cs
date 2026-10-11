namespace ClassroomAgent.Application.Models;

/// <summary>The current-student count of one course of the page (US-020 entity model §2, Q6).</summary>
public sealed record CourseListStudentCountRecord(long CourseId, int StudentCount);
