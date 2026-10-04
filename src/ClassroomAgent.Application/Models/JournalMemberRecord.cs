namespace ClassroomAgent.Application.Models;

/// <summary>One student membership of the course with its participant — a candidate row (US-025 entity model §3, db-design Q3).</summary>
public sealed record JournalMemberRecord(
    long ParticipantId,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    bool OnRoster,
    string? FullName,
    string? Email);
