using System.ComponentModel.DataAnnotations;
using ClassroomAgent.Contracts;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The rules of a parsed <see cref="AdminLoginCheckRequest"/> (US-008 spec VR-006). A valid UUID that no
/// installation has is valid input — the answer is then <c>404 unknown_installation</c>, not <c>400</c>.
/// Validation runs before any database access, and the rejected body is never logged (SC-10).
/// </summary>
public sealed class AdminLoginCheckInput
{
    [Required]
    public Guid? InstallationId { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(254, MinimumLength = 3)]
    [ServiceChannelEmail]
    public string? Email { get; init; }

    public static AdminLoginCheckInput From(AdminLoginCheckRequest request) =>
        new()
        {
            InstallationId = request.InstallationId,
            Email = request.Email?.Trim(),
        };

    public bool IsValid() => Validator.TryValidateObject(this, new ValidationContext(this), null, validateAllProperties: true);
}
