using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// VR-003 for a service-account client ID: 10 to 32 ASCII digits, kept as a string.
/// A missing value is left to <c>Required</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InstallationClientIdAttribute : ValidationAttribute
{
    private const int MinimumLength = 10;

    private const int MaximumLength = 32;

    public override bool IsValid(object? value) =>
        value is not string clientId
        || (clientId.Length is >= MinimumLength and <= MaximumLength && clientId.All(char.IsAsciiDigit));
}
