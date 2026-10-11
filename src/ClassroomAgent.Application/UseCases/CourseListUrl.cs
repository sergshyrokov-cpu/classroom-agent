using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Openapi US-020 <c>CourseListUrl</c>: the one rule that builds every URL the course-list page produces — paging links
/// and the language switcher's return path (spec FR-009, S-05, S-06).
/// </summary>
/// <remarks>Compile-only skeleton created at TEST_WRITING (US-020 OD-012 a); IMPLEMENTATION completes it.</remarks>
public static class CourseListUrl
{
    /// <summary>The page's path; equal to the host's route constant.</summary>
    public const string Path = "/workspace/courses";

    /// <summary>
    /// The path with <c>status</c>, <c>teacherId</c>, <c>q</c>, <c>page</c> in that order, each omitted at its default
    /// (<c>active</c>, none, none, 1); <c>q</c> percent-encoded as UTF-8.
    /// </summary>
    public static string Build(CourseStatusFilter status, long? teacherId, string? searchText, int page)
    {
        _ = (status, teacherId, searchText, page);
        throw new NotImplementedException();
    }
}
