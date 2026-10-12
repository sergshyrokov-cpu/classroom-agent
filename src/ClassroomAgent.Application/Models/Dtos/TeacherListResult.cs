namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-022 <c>TeacherListResult</c>: one page of listed teachers, or the empty state (spec FR-006, FR-008).</summary>
public sealed record TeacherListResult(
    bool IsEmpty,
    int TotalCount,
    int Page,
    int PageCount,
    int PageSize,
    IReadOnlyList<TeacherListRow> Rows);
