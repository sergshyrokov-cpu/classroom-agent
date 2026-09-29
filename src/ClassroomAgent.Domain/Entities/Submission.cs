using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// A student's submission of one <see cref="CourseWork"/> item (US-015 entity model §4; OD-005, OD-006). The pair
/// <see cref="State"/> / <see cref="RawState"/> changes only through <see cref="SetState"/>.
/// </summary>
/// <remarks>
/// TEST_WRITING compile-only skeleton (OD-012): every behaviour member below throws
/// <see cref="NotImplementedException"/>. IMPLEMENTATION replaces every one of them.
/// </remarks>
public sealed class Submission
{
    private Submission()
    {
        GoogleId = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>The owning <see cref="CourseWork"/>.</summary>
    public long CourseWorkId { get; private set; }

    /// <summary>The <see cref="ClassroomParticipant"/>, matched by Google <c>userId</c> (I-7).</summary>
    public long ParticipantId { get; private set; }

    /// <summary>Classroom's submission id.</summary>
    public string GoogleId { get; private set; }

    /// <summary>Closed vocabulary plus the <see cref="SubmissionState.Unrecognised"/> marker (§5).</summary>
    public SubmissionState State { get; private set; }

    /// <summary>The string Google sent; present only when <see cref="State"/> is <see cref="SubmissionState.Unrecognised"/>.</summary>
    public string? RawState { get; private set; }

    /// <summary>Raw points as given.</summary>
    public decimal? AssignedGrade { get; private set; }

    /// <summary>Raw points as given.</summary>
    public decimal? DraftGrade { get; private set; }

    /// <summary>The latest transition to <c>TURNED_IN</c>; absent when the history carries none.</summary>
    public DateTimeOffset? TurnedInAt { get; private set; }

    /// <summary>Google's flag, <see langword="false"/> when Google omits it.</summary>
    public bool Late { get; private set; }

    /// <summary>Google's, per OD-007.</summary>
    public DateTimeOffset? UpdateTime { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Submission Import(long courseWorkId, long participantId, string googleId, SubmissionDetails details) =>
        throw new NotImplementedException();

    /// <summary>The upsert's update half; the surrogate identity and <see cref="GoogleId"/> are untouched.</summary>
    public void UpdateFrom(SubmissionDetails details) => throw new NotImplementedException();

    /// <summary>
    /// The only way <see cref="State"/> and <see cref="RawState"/> change (OD-005, db-design §4.2): the
    /// biconditional it enforces is that <see cref="SubmissionState.Unrecognised"/> <b>requires</b> a raw string,
    /// and any other state <b>forbids</b> one.
    /// </summary>
    public void SetState(SubmissionState state, string? rawState) => throw new NotImplementedException();
}
