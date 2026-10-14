namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-043 openapi <c>BlankFormInputs</c>: the defaults and limits of the two per-copy inputs (spec FR-003, VR-001).</summary>
public sealed record BlankFormInputs(
    int StudentsPerCopyDefault,
    int StudentsPerCopyMax,
    int LessonsPerCopyDefault,
    int LessonsPerCopyMax);
