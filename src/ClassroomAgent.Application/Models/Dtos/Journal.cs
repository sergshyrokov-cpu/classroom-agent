namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>The journal of one course for a period (US-025 openapi <c>Journal</c>).</summary>
public sealed record Journal(IReadOnlyList<JournalColumn> Columns, IReadOnlyList<JournalRow> Rows);
