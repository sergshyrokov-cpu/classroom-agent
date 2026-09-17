using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// VR-001 and OD-002 for a school name, reporting the first failing rule as its translation
/// key: at most 200 code points, no whitespace at either edge, no <c>Cc</c> or <c>Cf</c>
/// character anywhere. Nothing is trimmed. A missing value is left to <c>Required</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InstallationNameAttribute : ValidationAttribute
{
    private const int MaximumLength = 200;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string name || name.Length == 0)
        {
            return ValidationResult.Success;
        }

        var runes = name.EnumerateRunes().ToList();
        var key = runes.Count > MaximumLength ? "Installation.Name.Length"
            : Rune.IsWhiteSpace(runes[0]) || Rune.IsWhiteSpace(runes[^1]) ? "Installation.Name.EdgeWhitespace"
            : runes.Any(IsControlOrFormat) ? "Installation.Name.InvalidCharacters"
            : null;

        return key is null ? ValidationResult.Success : new ValidationResult(key, [validationContext.MemberName ?? string.Empty]);
    }

    private static bool IsControlOrFormat(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format;
}
