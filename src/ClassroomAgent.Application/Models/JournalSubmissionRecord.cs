using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>One submission to an item of the period — the input of a cell (US-025 entity model §3, db-design Q4; OD-008).</summary>
public sealed record JournalSubmissionRecord(
    long Id,
    long ItemId,
    long ParticipantId,
    DateTimeOffset? UpdateTime,
    SubmissionState State,
    string? RawState,
    decimal? AssignedGrade,
    decimal? DraftGrade,
    DateTimeOffset? TurnedInAt,
    bool Late);
