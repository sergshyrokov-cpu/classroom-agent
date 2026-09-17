using System.ComponentModel.DataAnnotations;
using ClassroomAgent.Contracts;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The rules of a parsed <see cref="LegitimacyCheckRequest"/> (US-005 spec VR-002). A valid UUID that no
/// installation has is valid input, and so is a contract version the Control Plane does not support.
/// </summary>
public sealed class LegitimacyCheckInput
{
    [Required]
    public Guid? InstallationId { get; init; }

    [Required(AllowEmptyStrings = true)]
    [ApplicationVersion]
    public string? ApplicationVersion { get; init; }

    [Required]
    [Range(1, 999_999)]
    public int? ContractVersion { get; init; }

    public static LegitimacyCheckInput From(LegitimacyCheckRequest request) =>
        new()
        {
            InstallationId = request.InstallationId,
            ApplicationVersion = request.ApplicationVersion,
            ContractVersion = request.ContractVersion,
        };

    public bool IsValid() => Validator.TryValidateObject(this, new ValidationContext(this), null, validateAllProperties: true);
}
