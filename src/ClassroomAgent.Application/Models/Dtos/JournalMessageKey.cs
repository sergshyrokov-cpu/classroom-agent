namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>The closed list of validation messages (US-025 openapi <c>JournalMessageKey</c>, spec §6).</summary>
public enum JournalMessageKey
{
    CourseMalformed,
    CourseUnknown,
    FromMalformed,
    ToMalformed,
    PeriodInverted,
    ViewUnknown,
}
