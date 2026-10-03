namespace ClassroomAgent.Application.Models;

/// <summary>
/// A submission whose Classroom state is outside the six values the vocabulary holds (US-015 spec VR-004,
/// OD-005, OD-011).
/// </summary>
/// <param name="GoogleId">The Classroom submission id — an identifier, never a person or a grade (SC-10).</param>
/// <param name="State">The unrecognised state string, exactly as Google sent it.</param>
/// <remarks>
/// Unlike an unfamiliar <b>course</b> state, which skips the course (US-014 OD-010), the submission is
/// <b>stored</b> with the marker and this raw value: a missing submission would read as «не сдано» in a journal
/// (BR-056) and state something false about one student's work. The run reports it so the host can write the one
/// <c>Warning</c> line — the Application layer has no logger of its own (spec FR-017, the US-014
/// <see cref="SkippedCourse"/> precedent). Neither value is personal data.
/// </remarks>
public sealed record UnrecognisedSubmission(string GoogleId, string State);
