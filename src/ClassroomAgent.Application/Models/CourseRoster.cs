namespace ClassroomAgent.Application.Models;

/// <summary>
/// Both rosters of one course, each fully paged (US-014 entity model §6). A returned <see cref="CourseRoster"/>
/// means the read succeeded, even when a list is empty; an exception is what an unknown roster looks like
/// (I-6, I-7).
/// </summary>
/// <param name="Teachers">The course's teacher entries.</param>
/// <param name="Students">The course's student entries.</param>
public sealed record CourseRoster(IReadOnlyList<RosterEntry> Teachers, IReadOnlyList<RosterEntry> Students);
