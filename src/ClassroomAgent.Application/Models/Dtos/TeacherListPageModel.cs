namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-022 <c>TeacherListPageModel</c>: the effective status, the validation messages, the list (null on 400) and
/// the language switcher's return path. A rejected value appears nowhere in it.
/// </summary>
public sealed record TeacherListPageModel(
    CourseStatusFilter Status,
    IReadOnlyList<TeacherListMessageKey> MessageKeys,
    TeacherListResult? List,
    string ReturnPath);
