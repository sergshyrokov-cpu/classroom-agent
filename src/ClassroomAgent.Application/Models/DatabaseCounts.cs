namespace ClassroomAgent.Application.Models;

/// <summary>The eleven counters of the Database page, read in one statement (US-024 db-design §3.1). Counts only.</summary>
public sealed record DatabaseCounts(
    int ActiveCourses,
    int ArchivedCourses,
    int ProvisionedCourses,
    int DeclinedCourses,
    int SuspendedCourses,
    int Teachers,
    int Students,
    int CourseWorkItems,
    int Submissions,
    int MeetMeetings,
    int MeetCodesAwaitingDecision);
