namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-020 <c>CourseListResult</c>: one page of matching courses, or an empty state (spec FR-008, FR-011).</summary>
public sealed record CourseListResult(
    CourseListEmptyState? EmptyState,
    int TotalCount,
    int Page,
    int PageCount,
    int PageSize,
    IReadOnlyList<CourseListRow> Rows);
