using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// A report query read by <see cref="ReportRequestReader"/> (US-028 entity model §3.3): what the request selects and
/// every shape message found, in parameter order. Nothing here has been checked against the database yet.
/// </summary>
/// <param name="Template">The effective template reference — the built-in key when absent or malformed.</param>
/// <param name="TemplateValid">False when the template parameter was malformed.</param>
/// <param name="CreatedTemplateId">The id of a created template; null for the built-in one.</param>
/// <param name="PageNameSource">The page's valid <c>names</c> value; null when absent or malformed (US-042 FR-004).</param>
internal sealed record ReportSelection(
    string Template,
    bool TemplateValid,
    long? CreatedTemplateId,
    long? CourseId,
    DateOnly? From,
    DateOnly? To,
    ReportNameSource? PageNameSource,
    IReadOnlyList<ReportMessageKey> Messages);
