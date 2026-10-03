namespace ClassroomAgent.Application.Models;

/// <summary>
/// A unit of a purge run that rolled back (US-037 spec FR-004, FR-009, FR-012; OD-007): the step, the course's internal
/// id for a per-course step, and the exception <b>type</b> name — never its message, never a Google id.
/// </summary>
public sealed record RetentionPurgeFailure(RetentionPurgeStep Step, long? CourseId, string ExceptionType);
