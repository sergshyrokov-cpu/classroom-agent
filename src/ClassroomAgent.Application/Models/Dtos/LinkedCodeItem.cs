namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One code of the Linked list (US-032 OpenAPI <c>LinkedCodeItem</c>); times are school-local.</summary>
public sealed record LinkedCodeItem(
    string MeetingCode,
    CourseOption Course,
    bool MadeAutomatically,
    AccountLabel? MadeBy,
    DateTime MadeAt,
    AccountLabel? ConfirmedBy,
    DateTime? ConfirmedAt,
    bool CanConfirm,
    int MeetingCount,
    DateOnly? LastMeetingDate);
