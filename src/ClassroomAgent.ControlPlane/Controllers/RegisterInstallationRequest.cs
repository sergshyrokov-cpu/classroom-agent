using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The registration form (US-002 api-design §5). Only these fields are bound; a posted
/// status, identifier or creation time is ignored. Error messages are translation keys.
/// </summary>
public sealed class RegisterInstallationRequest
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "Installation.Name.Required")]
    [InstallationName]
    public string? Name { get; set; }

    [Required(AllowEmptyStrings = true, ErrorMessage = "Installation.Domain.Required")]
    [InstallationDomain]
    public string? Domain { get; set; }

    [Required(AllowEmptyStrings = true, ErrorMessage = "Installation.ClientId.Required")]
    [InstallationClientId(ErrorMessage = "Installation.ClientId.Format")]
    public string? ClientId { get; set; }

    /// <summary>Optional at registration (US-006 spec FR-002); empty means "not set".</summary>
    [InstallationPushAddress]
    public string? PushAddress { get; set; }
}
