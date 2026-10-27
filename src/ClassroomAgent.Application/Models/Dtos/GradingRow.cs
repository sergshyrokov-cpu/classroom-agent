namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>GradingRow</c>; <see cref="Left"/> is US-060 spec FR-006.</summary>
public sealed record GradingRow(
    PersonName Student,
    IReadOnlyList<ReportCell> Cells,
    LeftMark? Left = null);
