namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-028 entity model §3.2: the program texts of an export, resolved through <c>IReportTexts</c>.</summary>
public enum ReportText
{
    SheetGrading,
    SheetLessonTopics,
    HeaderTemplate,
    HeaderCourse,
    HeaderPeriod,
    HeaderTeachers,
    BuiltInTemplateName,
    StudentHeading,
    Material,
    Draft,
    TurnedInOn,
    StudentUnnamed,
    TeacherUnnamed,
    TopicDate,
    TopicTitle,
    TopicHours,
    TopicTeacher,
    TopicIndependentWork,
    TopicSignature,
    NothingPublished,
    NoStudents,
    CourseFallback,
}
