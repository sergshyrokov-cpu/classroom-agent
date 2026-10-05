namespace ClassroomAgent.Domain.Enums;

/// <summary>The template setting "names" (US-042 spec FR-002; entity model §1.1): where a person's displayed name comes from.</summary>
public enum ReportNameSource
{
    /// <summary>"Surname Name" from the Google profile, else the email part before <c>@</c>.</summary>
    Profile,

    /// <summary>The email part before <c>@</c>.</summary>
    Email,
}
