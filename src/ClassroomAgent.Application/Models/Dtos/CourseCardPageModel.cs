namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-021 <c>CourseCardPageModel</c>: sections already sorted (spec FR-007); counts are the lengths.</summary>
public sealed record CourseCardPageModel(
    CourseCardDetails Course,
    IReadOnlyList<CourseCardTeacher> Teachers,
    IReadOnlyList<CourseCardPerson> Students,
    IReadOnlyList<CourseCardLeaver> Leavers,
    string ReturnPath);
