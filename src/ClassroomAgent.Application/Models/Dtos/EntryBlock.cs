using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-065 <c>EntryBlock</c>: the typed email — kept only on a VR-001 re-render (api-design §2.4) — its field
/// error, the outcome when nothing was entered, and with "already loaded" the matching participants (at most 50) and
/// whether more matched (spec FR-004).
/// </summary>
public sealed record EntryBlock(
    string? Email,
    LiftFieldError? FieldError,
    ExclusionEntryOutcome? Outcome,
    IReadOnlyList<ParticipantSearchResult> LoadedParticipants,
    bool LoadedTruncated)
{
    /// <summary>An empty form.</summary>
    public static EntryBlock Empty { get; } = new(null, null, null, [], false);
}
