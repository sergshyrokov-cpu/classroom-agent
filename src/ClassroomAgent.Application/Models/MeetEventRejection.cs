namespace ClassroomAgent.Application.Models;

/// <summary>
/// Why a <c>call_ended</c> event was skipped (US-031 spec VR-001, FR-012): the rule it failed, or that the adapter
/// could not read it at all. Counted per reason for the Warning line; the event's values are never logged.
/// </summary>
public enum MeetEventRejection
{
    ConferenceId,
    MeetingCode,
    EndpointId,
    EventTime,
    Duration,
    Unreadable,
}
