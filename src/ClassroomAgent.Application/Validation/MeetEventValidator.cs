using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// US-031 spec VR-001: a <c>call_ended</c> event is external input (SC-10, §8), checked before anything is written. An
/// event failing a rule is skipped and counted under its <see cref="MeetEventRejection"/>; its values are never logged.
/// Leading and trailing whitespace is trimmed; values are otherwise kept as Google returned them.
/// </summary>
public static class MeetEventValidator
{
    /// <summary>
    /// The <c>identifier_type</c> Google documents for an email identifier (spec FR-003). Unverified on a live domain —
    /// <c>trebovaniya.md</c> §7 item 28, OD-009; any other value makes the connection an "other participant".
    /// </summary>
    public const string EmailIdentifierType = "email_address";

    /// <summary>How far outside the requested window an event time may lie (VR-001, I-7).</summary>
    public static readonly TimeSpan WindowTolerance = TimeSpan.FromDays(1);

    /// <summary>
    /// Null when the event passes every rule, with <paramref name="trimmed"/> holding its trimmed values; otherwise the
    /// first rule it fails, in the order of VR-001.
    /// </summary>
    public static MeetEventRejection? Check(
        MeetCallEndedEvent meetEvent,
        DateTimeOffset from,
        DateTimeOffset to,
        out MeetCallEndedEvent trimmed)
    {
        ArgumentNullException.ThrowIfNull(meetEvent);
        trimmed = meetEvent with
        {
            ConferenceId = meetEvent.ConferenceId?.Trim(),
            MeetingCode = meetEvent.MeetingCode?.Trim(),
            OrganizerEmail = meetEvent.OrganizerEmail?.Trim(),
            EndpointId = meetEvent.EndpointId?.Trim(),
            Identifier = meetEvent.Identifier?.Trim(),
            IdentifierType = meetEvent.IdentifierType?.Trim(),
        };

        if (!Within(trimmed.ConferenceId, MeetSession.ConferenceIdMaxLength))
        {
            return MeetEventRejection.ConferenceId;
        }

        if (!Within(trimmed.MeetingCode, MeetSession.MeetingCodeMaxLength))
        {
            return MeetEventRejection.MeetingCode;
        }

        if (!Within(trimmed.EndpointId, MeetParticipation.EndpointIdMaxLength))
        {
            return MeetEventRejection.EndpointId;
        }

        if (trimmed.OccurredAt is not { } occurredAt
            || occurredAt < from - WindowTolerance
            || occurredAt > to + WindowTolerance)
        {
            return MeetEventRejection.EventTime;
        }

        if (trimmed.DurationSeconds is not (>= 0 and <= MeetParticipation.MaxDurationSeconds))
        {
            return MeetEventRejection.Duration;
        }

        return null;
    }

    /// <summary>
    /// VR-001, FR-005: an organizer or participant address counts only when present, within the email maximum and well
    /// formed — exactly one <c>@</c> with a non-empty part before it (security review M-1); absent, longer or malformed
    /// is "not a domain account", never an invalid event.
    /// </summary>
    public static bool IsUsableEmail(string? email)
    {
        if (email is not { Length: > 0 and <= MeetSession.EmailMaxLength })
        {
            return false;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && email.IndexOf('@', at + 1) < 0;
    }

    private static bool Within(string? value, int maxLength) => !string.IsNullOrEmpty(value) && value.Length <= maxLength;
}
