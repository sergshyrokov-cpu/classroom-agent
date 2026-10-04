namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One row — a student of the period; carries no participant id (US-025 api-design §2.5).</summary>
public sealed record JournalRow(string? DisplayName, JournalNameKind NameKind, IReadOnlyList<JournalCell> Cells);
