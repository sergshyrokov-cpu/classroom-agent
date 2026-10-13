namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-024 openapi <c>DatabaseStatisticsPageModel</c> without the host's message key (entity model §4).</summary>
public sealed record DatabaseStatisticsView(
    CourseCounts Courses,
    int Teachers,
    int Students,
    int CourseWorkItems,
    int Submissions,
    int MeetMeetings,
    int MeetCodesAwaitingDecision,
    SynchronizationBlock Synchronization,
    FailureGuidance FailureGuidance);
