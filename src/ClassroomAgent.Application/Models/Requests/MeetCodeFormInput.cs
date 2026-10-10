namespace ClassroomAgent.Application.Models.Requests;

/// <summary>
/// A posted meet-code form as raw name/value pairs in posted order (US-032 api-design §2.8, as US-027
/// <see cref="ReportTemplateFormInput"/>): <c>courseId</c>, <c>expectedState</c>, <c>expectedCourseId</c>,
/// <c>returnPage</c> are validated in <c>Application</c>; a repeated field is malformed; other names are ignored.
/// </summary>
public sealed record MeetCodeFormInput(IReadOnlyList<KeyValuePair<string, string?>> Fields);
