namespace ClassroomAgent.Application.Models;

/// <summary>How the teacher-list page answers (US-022 api-design §2.4): 200 or 400.</summary>
public enum TeacherListPageOutcome
{
    /// <summary>200.</summary>
    Shown,

    /// <summary>400 — <c>status</c> and/or <c>page</c> malformed.</summary>
    Invalid,
}
