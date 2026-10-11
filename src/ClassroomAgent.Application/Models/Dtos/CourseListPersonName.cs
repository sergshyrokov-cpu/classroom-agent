namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-020 <c>PersonName</c> (renamed: US-027's <see cref="PersonName"/> already exists in this namespace): the
/// name rule of spec FR-007. <see cref="Text"/> is null exactly when <see cref="Kind"/> is
/// <see cref="CourseListPersonNameKind.NoName"/>.
/// </summary>
public sealed record CourseListPersonName(CourseListPersonNameKind Kind, string? Text);
