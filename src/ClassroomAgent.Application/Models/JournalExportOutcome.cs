namespace ClassroomAgent.Application.Models;

/// <summary>US-028 entity model §3.2: how a journal export request ended.</summary>
public enum JournalExportOutcome
{
    Exported,
    Invalid,
    NotFound,

    /// <summary>US-043 spec VR-002 (OD-011 a): the form would need more sheets than one file may have.</summary>
    TooManySheets,
}
