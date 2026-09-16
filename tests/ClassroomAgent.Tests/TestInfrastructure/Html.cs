using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Reads form inputs out of rendered HTML.</summary>
public static partial class Html
{
    public const string AntiforgeryFieldName = "__RequestVerificationToken";

    /// <summary>The value of the first input with that name (case-insensitive); empty when it has no value; null when absent.</summary>
    public static string? InputValue(string html, string name)
    {
        foreach (Match tag in InputTag().Matches(html))
        {
            var nameAttribute = Attribute(tag.Value, "name");
            if (nameAttribute is not null && string.Equals(nameAttribute, name, StringComparison.OrdinalIgnoreCase))
            {
                return Attribute(tag.Value, "value") ?? string.Empty;
            }
        }

        return null;
    }

    public static bool HasInput(string html, string name) => InputValue(html, name) is not null;

    /// <summary>The body with every Data Protection token value (antiforgery) replaced by a placeholder.</summary>
    public static string WithoutProtectedTokens(string html) => ProtectedToken().Replace(html, "{token}");

    private static string? Attribute(string tag, string attribute)
    {
        var match = Regex.Match(
            tag,
            @"\s" + attribute + @"\s*=\s*(""(?<v>[^""]*)""|'(?<v>[^']*)')",
            RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value) : null;
    }

    [GeneratedRegex(@"<input\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputTag();

    [GeneratedRegex(@"CfDJ8[A-Za-z0-9_\-]+")]
    private static partial Regex ProtectedToken();
}
