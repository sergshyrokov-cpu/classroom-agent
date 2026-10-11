namespace ClassroomAgent.Application.Models.Requests;

/// <summary>The raw input of GET /workspace/meet-reports/meetings/{meetingId} (spec VR-003, api-design §2.4).</summary>
public sealed record MeetReportMeetingRequest(
    string MeetingId,
    IReadOnlyList<string?> CourseId,
    IReadOnlyList<string?> From,
    IReadOnlyList<string?> To);
