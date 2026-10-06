using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// US-028 entity model §3.5: the file of an export, or the field errors that stopped it; <see cref="Summary"/> carries
/// the facts of the success log line (spec FR-012).
/// </summary>
public sealed record JournalExportResult(
    JournalExportOutcome Outcome,
    JournalExportFile? File,
    IReadOnlyList<ExportFieldError> Errors,
    JournalExportSummary? Summary = null);
