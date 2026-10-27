namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportCell</c>, already resolved by the template (spec FR-005.4);
/// <see cref="AboveMaximum"/> is US-060 spec FR-004.</summary>
public sealed record ReportCell(
    ReportCellContent Content,
    ReportGrade? Grade,
    ReportCellMark? Mark,
    string? RawState,
    ReportCellMark? Late,
    ReportGrade? DraftGrade,
    DateOnly? TurnedInOn,
    ReportAboveMaximum? AboveMaximum = null);
