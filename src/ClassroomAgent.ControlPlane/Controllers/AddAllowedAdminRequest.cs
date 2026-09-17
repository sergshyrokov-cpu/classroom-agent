using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The add-Admin form (US-003 api-design §5). Only the email is bound; a posted identifier,
/// installation, added-by or added-at value is ignored. Error messages are translation keys.
/// </summary>
public sealed class AddAllowedAdminRequest
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "AllowedAdmin.Email.Required")]
    [AllowedAdminEmail]
    public string? Email { get; set; }
}
