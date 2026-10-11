using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>US-021 Q2 — no Google id; IsOwner decided in the database (db-design §2).</summary>
public sealed record CourseCardMemberRecord(
    long ParticipantId,
    string? Surname,
    string? GivenName,
    string? Email,
    ClassroomRole Role,
    bool OnRoster,
    DateTimeOffset LastSeenAt,
    bool IsOwner);
