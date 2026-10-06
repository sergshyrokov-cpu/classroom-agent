namespace ClassroomAgent.Application.Models;

/// <summary>US-028 entity model §3.2: how a journal export request ended.</summary>
public enum JournalExportOutcome
{
    Exported,
    Invalid,
    NotFound,
}
