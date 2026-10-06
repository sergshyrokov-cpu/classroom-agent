using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>US-028 entity model §3.2: the file of an export, or the field errors that stopped it.</summary>
public sealed record JournalExportResult(
    JournalExportOutcome Outcome,
    JournalExportFile? File,
    IReadOnlyList<ExportFieldError> Errors);
