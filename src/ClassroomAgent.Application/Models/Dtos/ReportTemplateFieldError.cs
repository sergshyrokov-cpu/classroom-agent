namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>One field problem: the field name as posted (<c>name</c>, <c>scale[i].label</c> …), the key and, for scale rows, the 1-based row number.</summary>
public sealed record ReportTemplateFieldError(
    string Field,
    ReportTemplateFieldErrorKey Key,
    int? RowNumber);
