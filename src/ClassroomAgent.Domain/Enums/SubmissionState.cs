namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// A submission's state (US-015 entity model §5). Seven members: the six values OD-011 resolved the vocabulary
/// to, plus <see cref="Unrecognised"/> — OD-005's marker for a value Google sends that this program does not
/// recognise, deliberately not skipped the way an unfamiliar course state is (US-014 OD-010).
/// </summary>
/// <remarks>
/// Ordering note, learned from US-014's D-5: the member order is the one below and a test must not assume it
/// matches the alphabetical order of the stored codes.
/// </remarks>
public enum SubmissionState
{
    New,
    Created,
    TurnedIn,
    Returned,
    ReclaimedByStudent,
    StudentEditedAfterTurnIn,
    Unrecognised,
}
