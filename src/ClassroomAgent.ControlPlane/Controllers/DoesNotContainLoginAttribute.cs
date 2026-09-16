using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The password may neither equal nor contain the login, compared case-insensitively
/// (VR-003). Evaluated independently of the length rule; skipped while the login itself
/// is empty or shorter than the VR-001 minimum.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DoesNotContainLoginAttribute(string loginProperty) : ValidationAttribute
{
    private const int MinimumLoginLength = 4;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var login = validationContext.ObjectType.GetProperty(loginProperty)?.GetValue(validationContext.ObjectInstance) as string;
        if (value is not string password || login is null || login.Length < MinimumLoginLength)
        {
            return ValidationResult.Success;
        }

        return password.Contains(login, StringComparison.OrdinalIgnoreCase)
            ? new ValidationResult(ErrorMessage, [validationContext.MemberName ?? string.Empty])
            : ValidationResult.Success;
    }
}
