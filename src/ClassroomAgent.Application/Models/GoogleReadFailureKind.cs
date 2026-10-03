namespace ClassroomAgent.Application.Models;

/// <summary>The class of a final Google read failure (US-017 spec FR-001).</summary>
public enum GoogleReadFailureKind
{
    /// <summary><c>429</c>, <c>5xx</c>, a timeout or a dropped connection, still failing after every attempt.</summary>
    Transient,

    /// <summary>Delegation, technical account, key or API configuration — never retried.</summary>
    Configuration,

    /// <summary><c>404</c> on a request scoped to one course — the course is skipped.</summary>
    CourseGone,

    /// <summary>Anything else.</summary>
    Unexpected,
}
