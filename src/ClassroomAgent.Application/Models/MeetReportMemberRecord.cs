using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>One membership of the course with its participant; Email may be null (US-033 db-design §7, OD-012).</summary>
public sealed record MeetReportMemberRecord(
    long ParticipantId,
    ClassroomRole Role,
    string? Email,
    string? FullName,
    DateTimeOffset StartsAt,
    DateTimeOffset LastSeenAt,
    bool OnRoster);
