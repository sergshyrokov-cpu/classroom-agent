namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportMark</c>: <see cref="ProgramKey"/> is a <c>ReportCellState</c> member name or <c>Late</c>.</summary>
public sealed record ReportCellMark(
    ReportCellMarkKind Kind,
    string? ProgramKey,
    string? Text);
