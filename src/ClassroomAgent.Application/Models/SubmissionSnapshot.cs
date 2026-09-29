namespace ClassroomAgent.Application.Models;

/// <summary>
/// One submission as <see cref="ClassroomAgent.Application.Ports.IClassroomReader"/> reports it (US-015 entity
/// model §6). <see cref="State"/> stays the string Google sent, so the use case — not the adapter — applies
/// OD-005/OD-011's classification.
/// </summary>
/// <param name="GoogleId">The Classroom submission id.</param>
/// <param name="CourseWorkGoogleId">Which coursework or material this submission belongs to (OD-002).</param>
/// <param name="GoogleUserId">The submitting student's Google user id.</param>
/// <param name="State">The state as the string Google sent, not yet parsed into <c>SubmissionState</c>.</param>
/// <param name="AssignedGrade">Raw points as given (VR-007).</param>
/// <param name="DraftGrade">Raw points as given.</param>
/// <param name="TurnedInAt">The latest transition to <c>TURNED_IN</c>, extracted from the history in the adapter
/// (FR-009, OD-009).</param>
/// <param name="Late">Google's flag, <see langword="false"/> when Google omits it.</param>
/// <param name="UpdateTime">Google's update time (OD-007).</param>
public sealed record SubmissionSnapshot(
    string GoogleId,
    string CourseWorkGoogleId,
    string GoogleUserId,
    string State,
    decimal? AssignedGrade,
    decimal? DraftGrade,
    DateTimeOffset? TurnedInAt,
    bool Late,
    DateTimeOffset? UpdateTime);
