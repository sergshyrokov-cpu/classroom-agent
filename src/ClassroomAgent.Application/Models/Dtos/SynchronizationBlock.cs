namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-024 spec FR-005: the state and three times, wall-clock in the school's time zone.</summary>
public sealed record SynchronizationBlock(
    SynchronizationDisplayState State,
    DateTime? LastSuccessfulRunAt,
    DateTime? LastFullRunAt,
    DateTime? MeetLoadedUpTo);
