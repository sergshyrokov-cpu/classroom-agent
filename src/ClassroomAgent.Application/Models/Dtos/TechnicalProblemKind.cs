namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>TechnicalProblem.kind</c>: X1 … X6 of spec FR-001.3.</summary>
public enum TechnicalProblemKind
{
    LastRunFailed,
    NoRecentSuccess,
    NoSuccessYet,
    CourseListEmpty,
    CoursesNotVisible,
    ControlPlaneUnreachable,
    UnknownCourseState,
}
