namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>The closed list of cell states (US-025 openapi <c>JournalCellState</c>, spec FR-006).</summary>
public enum JournalCellState
{
    Empty,
    NotAssigned,
    Unrecognised,
    Grade,
    TurnedInNotGraded,
    ReturnedWithoutGrade,
    TurnedIn,
    Returned,
    NotTurnedIn,
    NotDueYet,
    NotTurnedInNoDueDate,
}
