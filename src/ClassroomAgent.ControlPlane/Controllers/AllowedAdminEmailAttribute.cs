using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// VR-001 for an AllowedAdmin email, reporting the first failing rule as its translation key, in
/// the order: at most 254 characters, exactly one <c>@</c> with a non-empty name part and domain
/// part, name part at most 64 characters, name part characters <c>A–Z a–z 0–9 . _ - '</c>, no dot
/// at an edge of the name part and no two dots in a row. Nothing is trimmed. The domain part is
/// compared with the installation's domain by the service (VR-002). A missing value is left to
/// <c>Required</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AllowedAdminEmailAttribute : ValidationAttribute
{
    private const int MaximumLength = 254;

    private const int MaximumNameLength = 64;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string email || email.Length == 0)
        {
            return ValidationResult.Success;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        var name = at < 0 ? string.Empty : email[..at];
        var key = email.Length > MaximumLength ? "AllowedAdmin.Email.Length"
            : at <= 0 || at == email.Length - 1 || email.IndexOf('@', at + 1) >= 0 ? "AllowedAdmin.Email.Format"
            : name.Length > MaximumNameLength ? "AllowedAdmin.Email.NameLength"
            : !name.All(IsAllowedNameCharacter) ? "AllowedAdmin.Email.NameCharacters"
            : name.StartsWith('.') || name.EndsWith('.') || name.Contains("..", StringComparison.Ordinal) ? "AllowedAdmin.Email.NameDots"
            : null;

        return key is null ? ValidationResult.Success : new ValidationResult(key, [validationContext.MemberName ?? string.Empty]);
    }

    private static bool IsAllowedNameCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '\'';
}
