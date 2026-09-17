using System.ComponentModel.DataAnnotations;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// VR-002 and OD-001 for a Google Workspace domain, reporting the first failing rule as its
/// translation key, in the order: length 3–253, characters <c>A–Z a–z 0–9 . -</c>, at least
/// one dot, label structure (none empty, 1–63 characters, no hyphen at an edge), no
/// <c>xn--</c> label. Any letter case is accepted. A missing value is left to <c>Required</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InstallationDomainAttribute : ValidationAttribute
{
    private const int MinimumLength = 3;

    private const int MaximumLength = 253;

    private const int MaximumLabelLength = 63;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string domain || domain.Length == 0)
        {
            return ValidationResult.Success;
        }

        var labels = domain.Split('.');
        var key = domain.Length is < MinimumLength or > MaximumLength ? "Installation.Domain.Length"
            : !domain.All(IsAllowedCharacter) ? "Installation.Domain.Characters"
            : labels.Length < 2 ? "Installation.Domain.NoDot"
            : !labels.All(IsWellFormedLabel) ? "Installation.Domain.Labels"
            : labels.Any(label => label.StartsWith("xn--", StringComparison.OrdinalIgnoreCase)) ? "Installation.Domain.Idn"
            : null;

        return key is null ? ValidationResult.Success : new ValidationResult(key, [validationContext.MemberName ?? string.Empty]);
    }

    private static bool IsAllowedCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '-';

    private static bool IsWellFormedLabel(string label) =>
        label.Length is > 0 and <= MaximumLabelLength && label[0] != '-' && label[^1] != '-';
}
