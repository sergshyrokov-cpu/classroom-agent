namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-028 entity model §3.2: what a <see cref="WorkbookCell"/> holds.</summary>
public enum WorkbookCellKind
{
    Empty,
    Text,
    Number,
    Date,

    /// <summary>US-044 entity model §3.1: a time of day.</summary>
    Time,

    /// <summary>US-044 entity model §3.1: a local date and time of the school.</summary>
    DateTime,
}
