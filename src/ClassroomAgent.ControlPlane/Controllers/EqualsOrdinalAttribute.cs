using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// The repeated password must equal the password exactly: ordinal, case-sensitive, no
/// trimming (VR-007). A missing repetition is left to <c>Required</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EqualsOrdinalAttribute(string otherProperty) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string repeated)
        {
            return ValidationResult.Success;
        }

        var other = validationContext.ObjectType.GetProperty(otherProperty)?.GetValue(validationContext.ObjectInstance) as string;
        return string.Equals(repeated, other, StringComparison.Ordinal)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage, [validationContext.MemberName ?? string.Empty]);
    }
}
