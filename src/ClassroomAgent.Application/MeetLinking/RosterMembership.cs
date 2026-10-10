using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.MeetLinking;

/// <summary>One observed course membership with its participant's email (US-032 spec FR-002, entity model §4).</summary>
public sealed record RosterMembership(
    long CourseId,
    ClassroomRole Role,
    string Email,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    bool OnRoster);
