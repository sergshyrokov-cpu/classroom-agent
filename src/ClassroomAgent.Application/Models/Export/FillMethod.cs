namespace ClassroomAgent.Application.Models.Export;

/// <summary>
/// US-043 spec FR-004, OD-010, I-3: the closed list of ways a form is filled. One item while it is the only one — not
/// shown on the page and not sent in the request.
/// </summary>
public enum FillMethod
{
    /// <summary>"Базовий" / "Basic": a set number of students and lessons per copy, the rest on the next copy.</summary>
    Basic,
}
