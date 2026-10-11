namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>View DTO of the reports page (US-033 openapi MeetReportsPageModel).</summary>
public sealed record MeetReportsPageModel(
    IReadOnlyList<CourseOption> Courses,
    bool NoCoursesStored,
    long? SelectedCourseId,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<MeetReportsMessageKey> MessageKeys,
    MeetReports? Reports);
