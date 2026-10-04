namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>GradingRow</c>.</summary>
public sealed record GradingRow(
    PersonName Student,
    IReadOnlyList<ReportCell> Cells);
