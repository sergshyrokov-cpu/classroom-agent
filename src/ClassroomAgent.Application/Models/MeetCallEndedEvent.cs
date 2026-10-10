namespace ClassroomAgent.Application.Models;

/// <summary>
/// One <c>call_ended</c> event: only the values spec FR-003 lists, raw as the adapter mapped them and still
/// unvalidated (spec VR-001). No telemetry, device, location, display name or <c>is_external</c> (AC-013).
/// </summary>
public sealed record MeetCallEndedEvent(
    DateTimeOffset? OccurredAt,
    string? ConferenceId,
    string? MeetingCode,
    string? OrganizerEmail,
    string? EndpointId,
    string? Identifier,
    string? IdentifierType,
    long? DurationSeconds);
