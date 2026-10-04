namespace ClassroomAgent.Application.Models;

/// <summary>How the journal page answers (US-025 api-design §5): <c>200</c>, <c>400</c> or <c>404</c>.</summary>
public enum JournalPageOutcome
{
    /// <summary><c>200</c> — form only, a journal, an empty journal, or no course stored.</summary>
    Shown,

    /// <summary><c>400</c> — a malformed parameter or an inverted period.</summary>
    Invalid,

    /// <summary><c>404</c> — a well-formed id of no stored course.</summary>
    CourseUnknown,
}
