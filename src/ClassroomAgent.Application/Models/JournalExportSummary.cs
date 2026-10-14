using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// US-028 spec FR-012: what the success log line of an export records — ids, the period, codes and counts only, never
/// a name, a title, a template text or file content (SC-10).
/// </summary>
public sealed record JournalExportSummary(
    string Template,
    long CourseId,
    DateOnly From,
    DateOnly To,
    ReportNameSource NameSource,
    NameSourceOrigin NameSourceOrigin,
    int RowCount,
    int TopicCount)
{
    /// <summary>US-043 spec FR-011: the layout of the file.</summary>
    public ExportLayout Layout { get; init; }

    /// <summary>US-043 spec FR-011: with the form, the fill method, the per-copy sizes and the number of sheets.</summary>
    public BlankFormExportFacts? BlankForm { get; init; }
}
