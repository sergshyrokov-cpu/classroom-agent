namespace ClassroomAgent.Application.Models;

/// <summary>How report 4 answers (US-033 api-design §5): 200, 400 or 404.</summary>
public enum MeetReportMeetingOutcome
{
    /// <summary>200.</summary>
    Shown,

    /// <summary>400 — courseId absent or malformed.</summary>
    Invalid,

    /// <summary>404 — unknown course, unknown meeting, or a meeting not of the course.</summary>
    NotFound,
}
