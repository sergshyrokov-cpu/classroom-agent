using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// One person's participation in one course (US-014 entity model §4; <c>trebovaniya.md</c> §3) — the entity
/// PC-8 requires between <see cref="Course"/> and <see cref="ClassroomParticipant"/>. Unique on
/// (<see cref="CourseId"/>, <see cref="ParticipantId"/>): one participation per person per course, the role a
/// field on it, not part of its identity (FR-007, I-9).
/// </summary>
/// <remarks>
/// The invariant the database cannot express lives here: <see cref="LastSeenAt"/> advances only while the
/// person is on the roster — <see cref="SeenAgain"/> may move it, <see cref="NotOnRoster"/> never does, and
/// <see cref="FirstSeenAt"/> is never rewritten (BR-051, FR-009, FR-010).
/// </remarks>
public sealed class CourseMembership
{
    private CourseMembership()
    {
        Course = null!;
        Participant = null!;
    }

    public long Id { get; private set; }

    public long CourseId { get; private set; }

    public Course Course { get; private set; }

    public long ParticipantId { get; private set; }

    public ClassroomParticipant Participant { get; private set; }

    /// <summary>A field, not part of identity (FR-007, I-9).</summary>
    public ClassroomRole Role { get; private set; }

    /// <summary>When synchronization first saw the person on this roster; never rewritten (FR-009).</summary>
    public DateTimeOffset FirstSeenAt { get; private set; }

    /// <summary>When synchronization last saw the person on this roster; advances while they are seen.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Whether the person is on the roster now.</summary>
    public bool OnRoster { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary><see cref="FirstSeenAt"/> = <see cref="LastSeenAt"/> = <paramref name="seenAt"/>, <see cref="OnRoster"/> = true.</summary>
    public static CourseMembership FirstSeen(
        Course course,
        ClassroomParticipant participant,
        ClassroomRole role,
        DateTimeOffset seenAt)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(participant);

        // Both the navigation and the foreign key are set: the key is what the in-memory doubles and a stored
        // parent read, and the navigation is what fixes the key up once EF Core generates the parent's identity.
        return new CourseMembership
        {
            Course = course,
            CourseId = course.Id,
            Participant = participant,
            ParticipantId = participant.Id,
            Role = role,
            FirstSeenAt = seenAt,
            LastSeenAt = seenAt,
            OnRoster = true,
        };
    }

    /// <summary>
    /// Advances <see cref="LastSeenAt"/> and refreshes <see cref="Role"/>; <see cref="FirstSeenAt"/> is never
    /// touched, including after an absence (FR-009). A <paramref name="seenAt"/> earlier than
    /// <see cref="LastSeenAt"/> is rejected.
    /// </summary>
    public void SeenAgain(ClassroomRole role, DateTimeOffset seenAt)
    {
        if (seenAt < LastSeenAt)
        {
            // Time does not run backwards within a run sequence, and ck_course_membership_seen_order would
            // refuse the row anyway (entity model §4.2, db-design §5.2).
            throw new ArgumentOutOfRangeException(
                nameof(seenAt),
                "A sighting cannot be earlier than the last one already recorded.");
        }

        Role = role;
        LastSeenAt = seenAt;
        OnRoster = true;
    }

    /// <summary>
    /// <see cref="OnRoster"/> = false; <see cref="LastSeenAt"/> is left where it was (FR-010). Takes no instant
    /// on purpose (entity-model §4.2).
    /// </summary>
    public void NotOnRoster() => OnRoster = false;
}
