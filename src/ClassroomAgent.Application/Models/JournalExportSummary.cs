using ClassroomAgent.Application.Models.Dtos;
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
    int TopicCount);
