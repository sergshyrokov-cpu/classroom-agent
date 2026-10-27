using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// One membership of the course, either role, with its participant (US-027 db-design F3). The participant's name
/// arrives as its two parts (US-042 entity model §3.2); the displayed name is computed in Application.
/// <see cref="SeenOnce"/>: first and last seen are equal (US-060 OD-003 a).
/// </summary>
public sealed record JournalCourseMemberRecord(
    long ParticipantId,
    ClassroomRole Role,
    DateTimeOffset StartsAt,
    DateTimeOffset LastSeenAt,
    bool OnRoster,
    string? Surname,
    string? GivenName,
    string? Email,
    bool SeenOnce = false);
