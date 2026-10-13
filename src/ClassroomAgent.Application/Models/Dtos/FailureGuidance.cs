namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-024 spec FR-006: what a failed run offers, by role; never a diagnosis.</summary>
public enum FailureGuidance
{
    None,
    AskAdmin,
    ConnectionPageLink,
}
