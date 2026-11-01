namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>The course-choice form for one code (US-032 OpenAPI <c>MeetCodeCourseChoicePageModel</c>).</summary>
public sealed record MeetCodeCourseChoicePageModel(
    string MeetingCode,
    MeetCodeExpectedState CurrentState,
    CourseOption? CurrentCourse,
    IReadOnlyList<CandidateCourse> Candidates,
    IReadOnlyList<CourseOption> OtherCourses,
    int ReturnPage,
    MeetCodeFieldError? FieldError)
{
    /// <summary>US-061 spec FR-003.2, OD-002 a: the mark of each marked course on the page, by course id.</summary>
    public IReadOnlyDictionary<long, CourseAbsence> Absences { get; init; } = new Dictionary<long, CourseAbsence>();
}
