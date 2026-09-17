using ClassroomAgent.ControlPlane.Services;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>View model of a suspend or resume confirmation page (US-004 api-design §5).</summary>
public sealed record InstallationStatusConfirmationPageModel(
    InstallationStatusConfirmationDto Installation,
    InstallationStatusTransition Transition)
{
    /// <summary>Translation key prefix: <c>Installation.Suspend</c> or <c>Installation.Resume</c>.</summary>
    public string KeyPrefix =>
        Transition == InstallationStatusTransition.Suspend ? "Installation.Suspend" : "Installation.Resume";

    public string DetailPath => $"/installations/{Installation.Identifier:D}";

    public string ActionPath =>
        DetailPath + (Transition == InstallationStatusTransition.Suspend ? "/suspension" : "/resumption");
}
