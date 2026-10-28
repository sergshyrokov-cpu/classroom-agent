using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// US-065 entity model §4: the outcome, the VR-001 field error when invalid, and — with
/// <see cref="ExclusionEntryOutcome.AlreadyLoaded"/> — the matching participants (at most 50) and whether more matched.
/// </summary>
public sealed record ExclusionEntryResult(
    ExclusionEntryOutcome Outcome,
    LiftFieldError? FieldError,
    IReadOnlyList<ParticipantSearchResult> LoadedParticipants,
    bool LoadedTruncated);
