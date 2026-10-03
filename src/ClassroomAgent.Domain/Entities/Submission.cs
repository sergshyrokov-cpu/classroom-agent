using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// One student's submission of one item (US-015 entity model §4; <c>trebovaniya.md</c> §3 "Submission"). It
/// stores exactly what PC-13 allows plus Google's update time (OD-007), and <b>nothing else</b> — no file, no
/// answer, no attachment, no grade history, no rubric grade (BR-059).
/// </summary>
/// <remarks>
/// The natural key is (<see cref="CourseWorkId"/>, <see cref="GoogleId"/>) per Specification v2, because a
/// Classroom submission id is unique only among the submissions of its own course work. Grades are kept as
/// <b>raw points exactly as Google gave them</b> (VR-007): no conversion to a school scale, no ceiling against
/// the item's maximum, and a grade on ungraded work is stored rather than discarded (I-8).
/// </remarks>
public sealed class Submission
{
    /// <summary>PC-3: Google-side identifiers are stored in their own bounded column.</summary>
    public const int MaxGoogleIdLength = 64;

    /// <summary>OD-005: the raw state string is kept only for an unrecognised value, so a short bound suffices.</summary>
    public const int MaxRawStateLength = 64;

    private Submission()
    {
        GoogleId = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>The owning <see cref="CourseWork"/>; part of the natural key (Specification v2).</summary>
    public long CourseWorkId { get; private set; }

    /// <summary>The submitting person, matched by Google <c>userId</c> and never by email (I-7).</summary>
    public long ParticipantId { get; private set; }

    /// <summary>Google's submission id. Unique only within its course work, hence the composite key.</summary>
    public string GoogleId { get; private set; }

    /// <summary>The closed vocabulary of VR-004, plus OD-005's marker.</summary>
    public SubmissionState State { get; private set; }

    /// <summary>The string Google sent, kept <b>only</b> when <see cref="State"/> is <see cref="SubmissionState.Unrecognised"/>.</summary>
    public string? RawState { get; private set; }

    /// <summary>Raw points as given (PC-13, VR-007).</summary>
    public decimal? AssignedGrade { get; private set; }

    /// <summary>Raw points as given; the student cannot see it yet (§3).</summary>
    public decimal? DraftGrade { get; private set; }

    /// <summary>The latest transition to <c>TURNED_IN</c> (BR-058); absent when the history carried none (FR-009).</summary>
    public DateTimeOffset? TurnedInAt { get; private set; }

    /// <summary>Google's flag as Google computed it, never recomputed from the due date (I-9).</summary>
    public bool Late { get; private set; }

    /// <summary>Google's update time (OD-007) — the value PC-11 needs for a course's last activity.</summary>
    public DateTimeOffset? UpdateTime { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates the row for a submission not seen before.</summary>
    public static Submission Import(long courseWorkId, long participantId, string googleId, SubmissionDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleId);
        ArgumentNullException.ThrowIfNull(details);

        var submission = new Submission
        {
            CourseWorkId = courseWorkId,
            ParticipantId = participantId,
            GoogleId = Cut(googleId.Trim(), MaxGoogleIdLength),
        };
        submission.Apply(details);
        return submission;
    }

    /// <summary>
    /// The upsert's update half (FR-010). A grade changed in Google replaces the stored one and the previous
    /// value is kept nowhere — the history of grade changes is Epic 12 (BR-059).
    /// </summary>
    public void UpdateFrom(SubmissionDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Apply(details);
    }

    /// <summary>
    /// The one place <see cref="State"/> and <see cref="RawState"/> change, enforcing OD-005's biconditional:
    /// <see cref="SubmissionState.Unrecognised"/> <b>requires</b> the string Google sent, and every other state
    /// <b>forbids</b> one. The database checks the same pair (db-design §4.2); both exist because a constraint can
    /// only fail a write, while the entity refuses to construct the contradiction at all.
    /// </summary>
    public void SetState(SubmissionState state, string? rawState)
    {
        var unrecognised = state == SubmissionState.Unrecognised;
        var hasRaw = !string.IsNullOrWhiteSpace(rawState);

        if (unrecognised != hasRaw)
        {
            throw new ArgumentException(
                "An unrecognised submission state requires the raw value Google sent, and a recognised one forbids it.",
                nameof(rawState));
        }

        State = state;
        RawState = unrecognised ? Cut(rawState!.Trim(), MaxRawStateLength) : null;
    }

    private static string Cut(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private void Apply(SubmissionDetails details)
    {
        SetState(details.State, details.RawState);
        AssignedGrade = details.AssignedGrade;
        DraftGrade = details.DraftGrade;
        TurnedInAt = details.TurnedInAt;
        Late = details.Late;
        UpdateTime = details.UpdateTime;
    }
}
