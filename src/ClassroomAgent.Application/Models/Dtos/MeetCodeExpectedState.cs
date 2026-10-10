namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>A code's state as a form carries it: <c>unassigned</c>, <c>linked</c>, <c>marked</c> (US-032 spec FR-013, VR-003).</summary>
public enum MeetCodeExpectedState
{
    Unassigned,
    Linked,
    Marked,
}
