namespace ClassroomAgent.Application.Models;

/// <summary>US-044 entity model §2.2: how a Meet export request ended.</summary>
public enum MeetExportOutcome
{
    Exported,
    Invalid,
    NotFound,
}
