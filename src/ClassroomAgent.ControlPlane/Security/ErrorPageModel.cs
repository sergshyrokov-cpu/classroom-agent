namespace ClassroomAgent.ControlPlane.Security;

/// <summary>View model of the error page: a translation key and, for 400, a link back.</summary>
public sealed record ErrorPageModel(string MessageKey, string? BackLink);
