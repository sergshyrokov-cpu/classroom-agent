namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One code of the Not a course list (US-032 OpenAPI <c>NotACourseCodeItem</c>); times are school-local.</summary>
public sealed record NotACourseCodeItem(
    string MeetingCode,
    AccountLabel MarkedBy,
    DateTime MarkedAt,
    int MeetingCount,
    DateOnly? LastMeetingDate);
