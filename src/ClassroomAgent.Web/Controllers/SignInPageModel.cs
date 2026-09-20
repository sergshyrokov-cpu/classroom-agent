namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// View DTO of the sign-in page (US-008 openapi <c>SignInPageModel</c>; AD-8). <see cref="RefusalKey"/> is the
/// translation key of the previous refusal, taken from TempData, or null on a first visit.
/// </summary>
public sealed record SignInPageModel(string? RefusalKey);
