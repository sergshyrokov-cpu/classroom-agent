namespace ClassroomAgent.Application.Models;

/// <summary>What a meet-code write ended in (US-032 api-design §2.7). Read-only mode is not an outcome: it throws.</summary>
public enum MeetCodeChangeOutcome
{
    CoursePicked,
    Relinked,
    MarkRemoved,
    Confirmed,
    Marked,
    Malformed,
    CodeNotFound,
    CourseNotFound,
    SameCourse,
    StateChanged,
}
