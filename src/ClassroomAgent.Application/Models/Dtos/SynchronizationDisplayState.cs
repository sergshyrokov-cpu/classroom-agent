namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-024 spec FR-005: the state line of the synchronization block; no row is <see cref="NotYet"/>.</summary>
public enum SynchronizationDisplayState
{
    NotYet,
    InProgress,
    Completed,
    Failed,
}
