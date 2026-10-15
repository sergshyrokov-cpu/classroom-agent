namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-038 <c>SearchBlock</c>: the submitted term (only in the response to that search), the VR-001 error,
/// whether a valid search ran, at most 50 results and whether more matched (spec FR-002, I-4).
/// </summary>
public sealed record SearchBlock(
    string? Term,
    SearchFieldError? FieldError,
    bool Performed,
    IReadOnlyList<ParticipantSearchResult> Results,
    bool Truncated);
