namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>The course-choice form for one code (US-032 OpenAPI <c>MeetCodeCourseChoicePageModel</c>).</summary>
public sealed record MeetCodeCourseChoicePageModel(
    string MeetingCode,
    MeetCodeExpectedState CurrentState,
    CourseOption? CurrentCourse,
    IReadOnlyList<CandidateCourse> Candidates,
    IReadOnlyList<CourseOption> OtherCourses,
    int ReturnPage,
    MeetCodeFieldError? FieldError);
