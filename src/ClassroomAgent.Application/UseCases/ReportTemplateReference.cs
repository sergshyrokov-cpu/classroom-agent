using System.Globalization;

namespace ClassroomAgent.Application.UseCases;

/// <summary>The template reference of US-027 spec VR-006 / api-design §2.2.</summary>
public static class ReportTemplateReference
{
    /// <summary>The fixed key of the built-in "Academic journal".</summary>
    public const string AcademicJournal = "academic-journal";

    /// <summary><see cref="long.MaxValue"/> has 19 digits.</summary>
    private const int MaxIdDigits = 19;

    /// <summary>How a reference reads (VR-006).</summary>
    public enum Kind
    {
        Malformed,
        BuiltIn,
        Created,
    }

    /// <summary>
    /// VR-006: the built-in key exactly, or ASCII decimal digits parsing to a positive 64-bit integer; anything else —
    /// absent, empty, signed, spaced, another case of the key — is malformed.
    /// </summary>
    public static Kind Parse(string? text, out long id)
    {
        id = 0;
        if (string.Equals(text, AcademicJournal, StringComparison.Ordinal))
        {
            return Kind.BuiltIn;
        }

        if (text is { Length: > 0 and <= MaxIdDigits }
            && text.All(char.IsAsciiDigit)
            && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            id = parsed;
            return Kind.Created;
        }

        return Kind.Malformed;
    }

    /// <summary>A created template's reference: its id as a decimal string.</summary>
    public static string Of(long id) => id.ToString(CultureInfo.InvariantCulture);
}
