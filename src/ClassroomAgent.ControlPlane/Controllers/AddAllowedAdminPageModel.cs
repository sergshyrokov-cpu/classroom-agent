using ClassroomAgent.ControlPlane.Services;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// View model of the add-Admin form: the installation, the email to show and, after a wrong-domain
/// refusal, the domain the message names (api-design §4).
/// </summary>
public sealed record AddAllowedAdminPageModel(AddAllowedAdminFormDto Installation, string? Email, string? WrongDomain);
