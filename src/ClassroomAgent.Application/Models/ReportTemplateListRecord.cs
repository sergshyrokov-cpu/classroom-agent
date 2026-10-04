namespace ClassroomAgent.Application.Models;

/// <summary>One created template for the list (US-027 db-design T1): a null email means the author account was deleted.</summary>
public sealed record ReportTemplateListRecord(
    long Id,
    string Name,
    DateTimeOffset UpdatedAt,
    string? AuthorEmail);
