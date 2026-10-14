using ClassroomAgent.Application.Models.Export;

namespace ClassroomAgent.Application.Models;

/// <summary>US-043 spec FR-011: what the success log line records of a form export — codes and counts only (SC-10).</summary>
public sealed record BlankFormExportFacts(FillMethod FillMethod, int StudentsPerCopy, int LessonsPerCopy, int SheetCount);
