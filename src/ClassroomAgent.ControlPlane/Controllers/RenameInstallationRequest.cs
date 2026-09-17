using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>The name correction form (US-002 api-design §5, VR-001).</summary>
public sealed class RenameInstallationRequest
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "Installation.Name.Required")]
    [InstallationName]
    public string? Name { get; set; }
}
