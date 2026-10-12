using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The teacher-list page to render and how it answers.</summary>
public sealed record TeacherListPageResult(TeacherListPageOutcome Outcome, TeacherListPageModel Page);
