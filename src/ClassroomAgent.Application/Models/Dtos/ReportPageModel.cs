namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportPageModel</c>; <see cref="NameSwitch"/> is set only with a report (US-042 spec FR-005).</summary>
public sealed record ReportPageModel(
    IReadOnlyList<ReportTemplateOption> Templates,
    string SelectedTemplate,
    IReadOnlyList<CourseOption> Courses,
    bool NoCoursesStored,
    long? SelectedCourseId,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<ReportMessageKey> MessageKeys,
    string ReturnPath,
    Report? Report,
    NameSourceSwitch? NameSwitch);
