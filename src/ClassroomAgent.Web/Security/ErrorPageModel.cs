namespace ClassroomAgent.Web.Security;

/// <summary>View model of the error page: a translation key and, for <c>400</c>, a link back (US-008 spec FR-018).</summary>
public sealed record ErrorPageModel(string MessageKey, string? BackLink);
