using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// One Google Meet conference organized by an account of the school's domain (US-031 entity model §1, spec FR-005,
/// FR-008; PC-12). Start is the earliest join of its connections, end the latest leave (I-2). A reconnect Google records
/// as another conference is another session (BR-063).
/// </summary>
/// <remarks>
/// The meeting code and the organizer are fixed when the session is stored and never change (I-4). There is no link to
/// a course or a participant of Classroom (PC-12); linking is US-032's.
/// </remarks>
public sealed class MeetSession
{
    public const int ConferenceIdMaxLength = 128;

    public const int MeetingCodeMaxLength = 64;

    public const int EmailMaxLength = 254;

    private readonly List<MeetParticipation> _participations = [];

    private MeetSession()
    {
    }

    public long Id { get; private set; }

    public string ConferenceId { get; private set; } = string.Empty;

    public string MeetingCode { get; private set; } = string.Empty;

    public string OrganizerEmail { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset EndedAt { get; private set; }

    public IReadOnlyCollection<MeetParticipation> Participations => _participations;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A new meeting with its first connection; start and end from it (spec FR-005, FR-008).</summary>
    public static MeetSession Store(
        string conferenceId,
        string meetingCode,
        string organizerEmail,
        MeetConnection firstConnection)
    {
        CheckValue(conferenceId, 1, ConferenceIdMaxLength, nameof(conferenceId));
        CheckValue(meetingCode, 1, MeetingCodeMaxLength, nameof(meetingCode));
        CheckValue(organizerEmail, 3, EmailMaxLength, nameof(organizerEmail));

        var session = new MeetSession
        {
            ConferenceId = conferenceId,
            MeetingCode = meetingCode,
            OrganizerEmail = organizerEmail,
        };
        session.Record(firstConnection);
        return session;
    }

    /// <summary>
    /// Adds a new endpoint or updates a known one, then recomputes start and end over all participations (spec FR-009,
    /// AC-002). The meeting code and the organizer are never changed (I-4).
    /// </summary>
    public void Record(MeetConnection connection)
    {
        MeetParticipation.Check(connection);

        var known = _participations.FirstOrDefault(p => string.Equals(p.EndpointId, connection.EndpointId, StringComparison.Ordinal));
        if (known is null)
        {
            _participations.Add(MeetParticipation.From(connection));
        }
        else
        {
            known.Apply(connection);
        }

        StartedAt = _participations.Min(p => p.JoinedAt);
        EndedAt = _participations.Max(p => p.LeftAt);
    }

    private static void CheckValue(string value, int minLength, int maxLength, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        if (value.Length < minLength || value.Length > maxLength)
        {
            throw new ArgumentException("The value is outside its length limits.", name);
        }
    }
}
