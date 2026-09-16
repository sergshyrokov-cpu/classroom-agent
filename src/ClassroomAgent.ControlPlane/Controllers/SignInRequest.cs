using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>The sign-in form (VR-005): both fields non-empty, no other rule.</summary>
public sealed class SignInRequest
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "SignIn.Login.Required")]
    public string? Login { get; set; }

    [Required(AllowEmptyStrings = true, ErrorMessage = "SignIn.Password.Required")]
    public string? Password { get; set; }
}
