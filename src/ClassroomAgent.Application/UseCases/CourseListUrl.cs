using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Openapi US-020 <c>CourseListUrl</c>: the one rule that builds every URL the course-list page produces — paging links
/// and the language switcher's return path (spec FR-009, S-05, S-06). Only validated values reach it.
/// </summary>
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
        var parts = new List<string>();
        if (status != CourseStatusFilter.Active)
        {
            parts.Add("status=" + Token(status));
        }

        if (teacherId is { } teacher)
        {
            parts.Add("teacherId=" + teacher.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(searchText))
        {
            parts.Add("q=" + Uri.EscapeDataString(searchText));
        }

        if (page > 1)
        {
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? Path : Path + "?" + string.Join('&', parts);
    }

    /// <summary>Api-design §2.2: the lower-case token of a status, as links emit it.</summary>
    public static string Token(CourseStatusFilter status) => status.ToString().ToLowerInvariant();
}
