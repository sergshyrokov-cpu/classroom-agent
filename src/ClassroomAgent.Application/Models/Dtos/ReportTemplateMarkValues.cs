namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>A mark as the form shows it: <c>kind</c> and <c>text</c> as strings.</summary>
public sealed record ReportTemplateMarkValues(
    string Kind,
    string? Text);
