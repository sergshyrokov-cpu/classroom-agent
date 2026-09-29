namespace ClassroomAgent.Application.Models;

/// <summary>
/// Both Classroom resources of one course, each fully paged (US-015 entity model §6). A returned
/// <see cref="CourseWorkPage"/> means both reads succeeded; an exception means the course's items are unknown.
/// </summary>
/// <param name="Items">Every coursework and material item of the course.</param>
public sealed record CourseWorkPage(IReadOnlyList<CourseWorkSnapshot> Items);
