namespace ClassroomAgent.Domain.Enums;

/// <summary>The closed set of cell states a report template maps (US-027 spec FR-003; openapi <c>ReportCellState</c>).</summary>
public enum ReportCellState
{
    TurnedInNotGraded,

    ReturnedWithoutGrade,

    TurnedIn,

    Returned,

    NotTurnedIn,

    NotDueYet,

    NotTurnedInNoDueDate,

    NotAssigned,

    Unrecognised,
}
