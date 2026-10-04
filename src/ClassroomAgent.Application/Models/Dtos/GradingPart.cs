namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>GradingPart</c>; no rows means "no students in this period".</summary>
public sealed record GradingPart(
    IReadOnlyList<GradingColumn> Columns,
    IReadOnlyList<GradingRow> Rows);
