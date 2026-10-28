namespace ClassroomAgent.Application.Models;

/// <summary>US-062 entity model §2.4: how a confirmation of deleted courses ended (read-only mode throws instead).</summary>
public enum CourseDeletionConfirmationOutcome
{
    Confirmed,
    Invalid,
    ListChanged,
}
