namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-020 <c>CourseListPageModel</c>: the effective filter values, the teacher choice, the validation messages,
/// the list (null on 400 and 404) and the language switcher's return path. A rejected value appears nowhere in it.
/// </summary>
public sealed record CourseListPageModel(
    CourseStatusFilter Status,
    IReadOnlyList<TeacherOption> TeacherChoice,
    long? SelectedTeacherId,
    string? SearchText,
    IReadOnlyList<CourseListMessageKey> MessageKeys,
    CourseListResult? List,
    string ReturnPath);
