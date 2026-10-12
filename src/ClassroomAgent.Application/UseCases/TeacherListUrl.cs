using System.Globalization;
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
    public static string Build(CourseStatusFilter status, int page)
    {
        var parts = new List<string>();
        if (status != CourseStatusFilter.Active)
        {
            parts.Add("status=" + CourseListUrl.Token(status));
        }

        if (page > 1)
        {
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? Path : Path + "?" + string.Join('&', parts);
    }
}
