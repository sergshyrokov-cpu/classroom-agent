using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>The client ID change form (US-002 api-design §5, VR-003).</summary>
public sealed class ChangeInstallationClientIdRequest
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "Installation.ClientId.Required")]
    [InstallationClientId(ErrorMessage = "Installation.ClientId.Format")]
    public string? ClientId { get; set; }
}
