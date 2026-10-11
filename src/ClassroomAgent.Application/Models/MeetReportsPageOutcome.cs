namespace ClassroomAgent.Application.Models;

/// <summary>How the reports page answers (US-033 api-design §5): 200, 400 or 404.</summary>
public enum MeetReportsPageOutcome
{
    /// <summary>200.</summary>
    Shown,

    /// <summary>400 — a malformed parameter or an inverted period.</summary>
    Invalid,

    /// <summary>404 — a well-formed id of no stored course.</summary>
    CourseUnknown,
}
