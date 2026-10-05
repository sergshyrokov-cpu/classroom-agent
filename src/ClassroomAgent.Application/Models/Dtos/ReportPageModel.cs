namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ReportPageModel</c>.</summary>
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
    Report? Report)
{
    /// <summary>US-042 skeleton (OD-001): the name-source switch; null unless a report is shown (spec FR-005).</summary>
    public NameSourceSwitch? NameSwitch => throw new NotImplementedException();
}
