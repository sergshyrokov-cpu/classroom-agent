namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi <c>GradingRow</c>; <see cref="Left"/> is US-060 spec FR-006; <see cref="NamesakeEmail"/> is US-066 api-design
/// D-1, on the row so a teacher can never carry it (D-2).
/// </summary>
public sealed record GradingRow(
    PersonName Student,
    IReadOnlyList<ReportCell> Cells,
    LeftMark? Left = null,
    string? NamesakeEmail = null);
