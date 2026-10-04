namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportTemplateListItem</c>; <see cref="ChangedAt"/> is local time in the school's zone.</summary>
public sealed record ReportTemplateListItem(
    string Reference,
    bool IsBuiltIn,
    string? Name,
    ReportAuthorState? AuthorState,
    string? AuthorEmail,
    DateTime? ChangedAt,
    bool CanChange);
