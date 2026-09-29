using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// A submission's fields other than its identity (US-015 entity model §4.1), mirroring how
/// <see cref="CourseWorkDetails"/> splits identity from details. It lives in <c>Domain</c> because
/// <see cref="Submission"/> takes it and <c>Domain</c> references nothing (AD-3).
/// </summary>
/// <param name="State">Closed vocabulary plus the <see cref="SubmissionState.Unrecognised"/> marker (OD-005, OD-011).</param>
/// <param name="RawState">The string Google sent; present only when <paramref name="State"/> is
/// <see cref="SubmissionState.Unrecognised"/> (OD-005).</param>
/// <param name="AssignedGrade">Raw points as given (VR-007, I-8).</param>
/// <param name="DraftGrade">Raw points as given.</param>
/// <param name="TurnedInAt">The latest transition to <c>TURNED_IN</c>; absent when the history carries none
/// (BR-058, OD-009).</param>
/// <param name="Late">Google's flag, <see langword="false"/> when Google omits it.</param>
/// <param name="UpdateTime">Google's update time (OD-007).</param>
public sealed record SubmissionDetails(
    SubmissionState State,
    string? RawState,
    decimal? AssignedGrade,
    decimal? DraftGrade,
    DateTimeOffset? TurnedInAt,
    bool Late,
    DateTimeOffset? UpdateTime);
