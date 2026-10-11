using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The course-list page to render and how it answers.</summary>
public sealed record CourseListPageResult(CourseListPageOutcome Outcome, CourseListPageModel Page);
