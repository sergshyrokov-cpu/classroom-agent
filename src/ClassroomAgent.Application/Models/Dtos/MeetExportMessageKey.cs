namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-044 api-design §2.3: the message of a Meet export error.</summary>
public enum MeetExportMessageKey
{
    CourseMalformed,
    CourseUnknown,
    FromMalformed,
    ToMalformed,
    PeriodInverted,
    MeetingMalformed,
    MeetingNotFound,
    OrientationInvalid,
}
