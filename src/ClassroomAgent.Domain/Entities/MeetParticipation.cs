using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// One connection to a stored Meet conference, keyed by (conference, endpoint) (US-031 entity model §2, PC-3, PC-12).
/// The email is held only for a domain account; null marks an "other participant", who has no email and no name.
/// </summary>
/// <remarks>Created and updated only through <see cref="MeetSession.Record"/>, so it never exists without its session.</remarks>
public sealed class MeetParticipation
{
    public const int EndpointIdMaxLength = 128;

    public const int MaxDurationSeconds = 86_400;

    private MeetParticipation()
    {
    }

    public long Id { get; private set; }

    public long MeetSessionId { get; private set; }

    public string EndpointId { get; private set; } = string.Empty;

    /// <summary>A domain account's email; null = other participant (spec FR-008, AC-005).</summary>
    public string? Email { get; private set; }

    /// <summary>The "other participant" mark is the absence of an email (db-design §3.1); not mapped.</summary>
    public bool IsOtherParticipant => Email is null;

    public DateTimeOffset JoinedAt { get; private set; }

    public int DurationSeconds { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The instant the connection left the call: join plus duration (spec I-2).</summary>
    internal DateTimeOffset LeftAt => JoinedAt + TimeSpan.FromSeconds(DurationSeconds);

    internal static MeetParticipation From(MeetConnection connection)
    {
        Check(connection);
        return new MeetParticipation
        {
            EndpointId = connection.EndpointId,
            Email = connection.DomainEmail,
            JoinedAt = connection.JoinedAt,
            DurationSeconds = connection.DurationSeconds,
        };
    }

    /// <summary>A known endpoint read again: updated with the values read now (spec FR-009).</summary>
    internal void Apply(MeetConnection connection)
    {
        Check(connection);
        Email = connection.DomainEmail;
        JoinedAt = connection.JoinedAt;
        DurationSeconds = connection.DurationSeconds;
    }

    /// <summary>The invariants of <c>ck_meet_participation_*</c> (db-design §3.2).</summary>
    internal static void Check(MeetConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.EndpointId, nameof(connection));
        if (connection.EndpointId.Length > EndpointIdMaxLength)
        {
            throw new ArgumentException("The endpoint id is too long.", nameof(connection));
        }

        if (connection.DomainEmail is { } email && (email.Length < 3 || email.Length > MeetSession.EmailMaxLength))
        {
            throw new ArgumentException("The email is outside its length limits.", nameof(connection));
        }

        if (connection.DurationSeconds is < 0 or > MaxDurationSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(connection), connection.DurationSeconds, null);
        }
    }
}
