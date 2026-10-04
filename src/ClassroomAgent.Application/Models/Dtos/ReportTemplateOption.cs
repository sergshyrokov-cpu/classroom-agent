namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportTemplateOption</c>.</summary>
public sealed record ReportTemplateOption(
    string Reference,
    bool IsBuiltIn,
    string? Name);
