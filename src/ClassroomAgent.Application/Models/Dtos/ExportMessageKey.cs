namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-028 entity model §3.2: the translation key of an export field error.</summary>
public enum ExportMessageKey
{
    TemplateMalformed,
    TemplateNotFound,
    CourseMalformed,
    CourseUnknown,
    FromMalformed,
    ToMalformed,
    PeriodInverted,
    NameSourceMalformed,
    OrientationInvalid,
}
