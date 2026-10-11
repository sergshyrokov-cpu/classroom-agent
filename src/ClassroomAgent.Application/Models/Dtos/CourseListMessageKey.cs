namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-020 <c>CourseListMessageKey</c>: closed list, in parameter order (api-design §2.4, §5).</summary>
public enum CourseListMessageKey
{
    StatusMalformed,

    TeacherMalformed,

    TeacherUnknown,

    SearchRepeated,

    SearchTooLong,

    SearchControlCharacter,

    PageMalformed,
}
