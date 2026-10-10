namespace ClassroomAgent.Application.MeetLinking;

/// <summary>One stored meeting of a code: organizer, start and the emails of its domain-account participants (US-032 spec FR-003).</summary>
public sealed record MeetCodeMeeting(
    long MeetSessionId,
    string OrganizerEmail,
    DateTimeOffset StartedAt,
    IReadOnlyList<string> ParticipantEmails);
