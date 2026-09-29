using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-015 VR-003, VR-004, VR-007, I-8, I-9: what <see cref="Submission"/> stores as given and the one biconditional
/// <see cref="Submission.SetState"/> enforces — <see cref="SubmissionState.Unrecognised"/> requires the raw string
/// Google sent, and any other state forbids one (db-design §4.2, OD-005).
/// </summary>
public sealed class SubmissionInvariantTests
{
    private static readonly DateTimeOffset SomeInstant = InstallationTestHost.DefaultStart;

    private static SubmissionDetails Details(
        SubmissionState state = SubmissionState.New,
        string? rawState = null,
        decimal? assignedGrade = null,
        decimal? draftGrade = null,
        DateTimeOffset? turnedInAt = null,
        bool late = false,
        DateTimeOffset? updateTime = null) =>
        new(state, rawState, assignedGrade, draftGrade, turnedInAt, late, updateTime);

    /// <summary>I-8, VR-007: a grade Google reports on ungraded work is stored as given, never discarded.</summary>
    [Fact]
    public void GradeOnUngradedWork_IsStoredAsGiven()
    {
        var submission = Submission.Import(
            1,
            1,
            CourseWorkTestData.SubmissionId(1),
            Details(state: SubmissionState.TurnedIn, assignedGrade: 42m));

        Assert.Equal(42m, submission.AssignedGrade);
    }

    /// <summary>
    /// OD-005, db-design §4.2: <see cref="SubmissionState.Unrecognised"/> without the raw string Google sent is
    /// refused — the marker with nothing to identify would be the exact gap OD-005 exists to close.
    /// </summary>
    [Fact]
    public void UnrecognisedState_RequiresTheRawString()
    {
        var submission = Submission.Import(1, 1, CourseWorkTestData.SubmissionId(1), Details());

        Assert.Throws<ArgumentException>(() => submission.SetState(SubmissionState.Unrecognised, null));
    }

    /// <summary>OD-005, db-design §4.2: any recognised state carrying a leftover raw string is refused.</summary>
    [Fact]
    public void RecognisedState_ForbidsARawString()
    {
        var submission = Submission.Import(1, 1, CourseWorkTestData.SubmissionId(1), Details());

        Assert.Throws<ArgumentException>(() => submission.SetState(SubmissionState.New, CourseWorkTestData.UnrecognisedState));
    }

    /// <summary>
    /// I-9: the adapter has already reduced a missing Google <c>late</c> flag to <see langword="false"/> before it
    /// crosses the port (entity model §4.1), so the entity's job is to store <see cref="SubmissionDetails.Late"/>
    /// exactly as given, never recomputing it from the due date.
    /// </summary>
    [Fact]
    public void LateDefaultsToFalseWhenGoogleOmitsIt()
    {
        var submission = Submission.Import(1, 1, CourseWorkTestData.SubmissionId(1), Details(late: false));

        Assert.False(submission.Late);
    }
}
