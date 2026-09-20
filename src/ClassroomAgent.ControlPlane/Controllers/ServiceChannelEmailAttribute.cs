using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// "A syntactically valid address" as US-008 VR-006 states it, and nothing more: exactly one <c>@</c>, a
/// non-empty name part and domain part, and no whitespace anywhere. The length limit is
/// <see cref="StringLengthAttribute"/>'s and a missing value is <see cref="RequiredAttribute"/>'s.
/// </summary>
/// <remarks>
/// Deliberately <b>not</b> <see cref="AllowedAdminEmailAttribute"/>: that is US-003's stricter rule for the
/// address an Owner <em>adds</em> — 64 characters in the name part, a closed character set — and applying it here
/// would refuse with <c>400</c> an address the Control Plane is merely being asked about. VR-006 fixes only the
/// two rules above, so only they are checked, and a question about an address no entry could have is answered
/// <c>allowed: false</c> rather than rejected.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ServiceChannelEmailAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string email || email.Length == 0)
        {
            return ValidationResult.Success;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        var valid = at > 0
            && at < email.Length - 1
            && email.IndexOf('@', at + 1) < 0
            && !email.Any(char.IsWhiteSpace);

        return valid
            ? ValidationResult.Success
            : new ValidationResult("Not a syntactically valid address.", [validationContext.MemberName ?? string.Empty]);
    }
}
