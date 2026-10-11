namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One identified person in report 4, with capped seconds (spec FR-006).</summary>
public sealed record MeetingPersonLine(PersonRef Person, MeetParticipantMark Mark, long Seconds);
