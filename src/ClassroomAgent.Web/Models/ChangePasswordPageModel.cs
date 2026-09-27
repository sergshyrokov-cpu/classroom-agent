namespace ClassroomAgent.Web.Models;

/// <summary>
/// The view model of both password forms (US-012 openapi <c>ChangePasswordPageModel</c>): the forced change
/// after step 5 of the sign-in sequence, and the Dean's own later change. It carries no password, in any field,
/// in any direction (spec S-10).
/// </summary>
public sealed record ChangePasswordPageModel(bool Forced, string? MessageKey);
