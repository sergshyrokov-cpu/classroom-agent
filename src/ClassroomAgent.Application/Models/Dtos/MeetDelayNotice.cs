namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Spec FR-012: the Meet watermark as a local date-time in the school's zone; null = not loaded yet.</summary>
public sealed record MeetDelayNotice(DateTime? MeetLoadedUpTo);
