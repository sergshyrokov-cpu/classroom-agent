using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Openapi US-022 <c>TeacherListUrl</c>: the one rule that builds every URL the teacher-list page produces — paging links
/// and the language switcher's return path (spec FR-009, S-05, S-06). Only validated values reach it.
/// </summary>
public static class TeacherListUrl
{
    /// <summary>The page's path; equal to the host's route constant.</summary>
    public const string Path = "/workspace/teachers";

    /// <summary>The path with <c>status</c>, <c>page</c> in that order, each omitted at its default (<c>active</c>, 1).</summary>
    public static string Build(CourseStatusFilter status, int page) =>
        throw new NotImplementedException("US-022 skeleton (OD-010): completed at IMPLEMENTATION.");
}
