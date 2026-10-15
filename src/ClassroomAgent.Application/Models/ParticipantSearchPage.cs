namespace ClassroomAgent.Application.Models;

/// <summary>US-038 entity model §7: the rows a search read, at most the limit asked for.</summary>
public sealed record ParticipantSearchPage(IReadOnlyList<ParticipantSearchRecord> Participants);
