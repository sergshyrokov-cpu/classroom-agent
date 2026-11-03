using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>The Meet meetings page (US-032 OpenAPI <c>MeetCodesPageModel</c>): the selected list's page fills its own collection; the other two are empty. US-070: <see cref="SearchMessage"/> set means nothing was read, and <see cref="Counts"/> is null exactly then.</summary>
public sealed record MeetCodesPageModel(
    MeetCodeList List,
    MeetCodeListCounts? Counts,
    IReadOnlyList<UnassignedCodeItem> Unassigned,
    IReadOnlyList<LinkedCodeItem> Linked,
    IReadOnlyList<NotACourseCodeItem> NotACourse,
    int Page,
    int Size,
    long TotalElements,
    int TotalPages,
    MeetCodesMessageKey? Message,
    string? SearchText,
    SearchRefusalKey? SearchMessage);
