namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportTemplateFieldErrorKey</c>; each maps to <c>ReportTemplate.Validation.&lt;member&gt;</c>.</summary>
public enum ReportTemplateFieldErrorKey
{
    NameRequired,

    NameTooLong,

    NameNotUnique,

    TextInvalidCharacters,

    MarkTextRequired,

    MarkTextTooLong,

    ScaleNoRows,

    ScaleTooManyRows,

    ScaleBoundInvalid,

    ScaleRowInverted,

    ScaleLabelRequired,

    ScaleLabelTooLong,

    ScaleOverlap,

    ScaleGap,

    HoursRequired,

    HoursOutOfRange,
}
