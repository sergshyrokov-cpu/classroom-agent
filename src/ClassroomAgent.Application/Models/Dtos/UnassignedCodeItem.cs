namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One code of the Unassigned list (US-032 OpenAPI <c>UnassignedCodeItem</c>); dates in the school's zone.</summary>
public sealed record UnassignedCodeItem(
    string MeetingCode,
    IReadOnlyList<string> OrganizerEmails,
    DateOnly FirstMeetingDate,
    DateOnly LastMeetingDate,
    int MeetingCount,
    int ParticipantCount,
    IReadOnlyList<CandidateCourse> Candidates);
