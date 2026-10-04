using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>One membership of the course, either role, with its participant (US-027 db-design F3).</summary>
public sealed record JournalCourseMemberRecord(
    long ParticipantId,
    ClassroomRole Role,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    bool OnRoster,
    string? FullName,
    string? Email);
