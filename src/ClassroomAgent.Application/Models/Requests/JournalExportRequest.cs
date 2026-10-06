namespace ClassroomAgent.Application.Models.Requests;

/// <summary>US-028 entity model §3.2: the raw query of a journal export, validated in the use case.</summary>
public sealed record JournalExportRequest(
    string? Template,
    string? CourseId,
    string? From,
    string? To,
    string? Names,
    string? Orientation);
