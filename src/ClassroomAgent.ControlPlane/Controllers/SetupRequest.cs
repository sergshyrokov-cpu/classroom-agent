using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The setup form (api-design §5). Field rules VR-001, VR-002, VR-003 and VR-007 are
/// checked at binding; the setup code has no binding rule (OD-004). Error messages are
/// translation keys.
/// </summary>
public sealed class SetupRequest
{
    public string? SetupCode { get; set; }

    [Required(ErrorMessage = "Setup.Login.Required")]
    [StringLength(64, MinimumLength = 4, ErrorMessage = "Setup.Login.Length")]
    [RegularExpression("^[A-Za-z0-9._-]*$", ErrorMessage = "Setup.Login.Characters")]
    public string? Login { get; set; }

    [Required(AllowEmptyStrings = true, ErrorMessage = "Setup.Password.Required")]
    [CodePointLength(15, 128, ErrorMessage = "Setup.Password.Length")]
    [DoesNotContainLogin(nameof(Login), ErrorMessage = "Setup.Password.ContainsLogin")]
    public string? Password { get; set; }

    [Required(AllowEmptyStrings = true, ErrorMessage = "Setup.PasswordConfirmation.Required")]
    [EqualsOrdinal(nameof(Password), ErrorMessage = "Setup.PasswordConfirmation.Mismatch")]
    public string? PasswordConfirmation { get; set; }
}
