using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The course-choice form, or null when the code is unknown or malformed (US-032 OpenAPI: <c>404</c> / <c>400</c>).</summary>
public sealed record MeetCodeChoiceResult(bool Malformed, MeetCodeCourseChoicePageModel? Model);
