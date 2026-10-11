namespace ClassroomAgent.Application.Models.Requests;

/// <summary>US-021 api-design §2.3: the raw path value, parsed and validated in Application (spec VR-001).</summary>
public sealed record CourseCardRequest(string? CourseId);
