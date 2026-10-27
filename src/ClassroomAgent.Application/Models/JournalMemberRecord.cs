namespace ClassroomAgent.Application.Models;

/// <summary>
/// One student membership of the course with its participant — a candidate row (US-025 entity model §3, db-design Q3).
/// <see cref="SeenOnce"/>: first and last seen are equal (US-060 OD-003 a).
/// </summary>
public sealed record JournalMemberRecord(
    long ParticipantId,
    DateTimeOffset StartsAt,
    DateTimeOffset LastSeenAt,
    bool OnRoster,
    string? FullName,
    string? Email,
    bool SeenOnce = false);
