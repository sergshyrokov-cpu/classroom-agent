namespace ClassroomAgent.Application.Models;

/// <summary>US-062 entity model §2.2, db-design §5.3: one item marked "not in Classroom" (T2).</summary>
public sealed record ItemNotInClassroomRow(long CourseId, string Title, DateTimeOffset Since);
