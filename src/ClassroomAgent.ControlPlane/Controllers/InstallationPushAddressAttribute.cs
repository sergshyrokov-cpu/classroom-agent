using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// VR-001 for the push address: optional, and when present validated by <see cref="PushAddressRules"/>,
/// which reports the first failing rule as its translation key. The rejected value never leaves the form
/// (SC-10).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InstallationPushAddressAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        PushAddressRules.Validate(value as string, out _) is { } key
            ? new ValidationResult(key, [validationContext.MemberName ?? string.Empty])
            : ValidationResult.Success;
}
