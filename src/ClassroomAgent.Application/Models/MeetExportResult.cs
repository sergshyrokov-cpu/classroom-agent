using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// US-044 entity model §2.2: the file of a Meet export, or the field errors that stopped it; <see cref="Summary"/>
/// carries the facts of the success log line (spec FR-014).
/// </summary>
public sealed record MeetExportResult(
    MeetExportOutcome Outcome,
    JournalExportFile? File,
    IReadOnlyList<MeetExportFieldError> Errors,
    MeetExportSummary? Summary = null);
