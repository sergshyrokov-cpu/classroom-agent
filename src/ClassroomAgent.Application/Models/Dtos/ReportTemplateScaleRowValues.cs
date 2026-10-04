namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>A scale row as the form shows it, as strings.</summary>
public sealed record ReportTemplateScaleRowValues(
    string From,
    string To,
    string Label);
