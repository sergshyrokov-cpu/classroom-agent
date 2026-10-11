namespace ClassroomAgent.Application.Models;

/// <summary>How the course-list page answers (US-020 api-design §5): 200, 400 or 404.</summary>
public enum CourseListPageOutcome
{
    /// <summary>200.</summary>
    Shown,

    /// <summary>400 — one or more parameters malformed.</summary>
    Invalid,

    /// <summary>404 — every parameter well-formed, the teacher not in the choice.</summary>
    TeacherUnknown,
}
