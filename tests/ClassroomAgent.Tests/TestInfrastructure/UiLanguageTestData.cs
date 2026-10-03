using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Everything the US-039 tests and the future implementation must agree on: the action's path and form fields
/// (openapi <c>ChooseUiLanguageForm</c>), the translation keys of the switcher and how a rendered page reveals
/// its language and its switcher. One place, so the tests and the screens cannot drift (the US-012 pattern).
/// </summary>
public static partial class UiLanguageTestData
{
    /// <summary>The one operation, on both hosts (openapi <c>/account/language</c>).</summary>
    public const string ChoosePath = "/account/language";

    public const string Ukrainian = "uk";

    public const string English = "en";

    /// <summary>A rejected value that is unmistakable in a log file (spec AC-005, SC-10).</summary>
    public const string RejectedMarker = "rejected-language-marker-7f3c";

    public static class Fields
    {
        public const string Language = "language";

        public const string ReturnPath = "returnPath";
    }

    /// <summary>
    /// Translation keys of the switcher (spec FR-010, AC-008). Each host's two files carry all three; the two
    /// language labels are the same in both files, because each language names itself.
    /// </summary>
    public static class Keys
    {
        public const string UkrainianLabel = "Layout.Language.Uk";

        public const string EnglishLabel = "Layout.Language.En";

        public const string SwitcherName = "Layout.Language.Switcher";

        public static readonly string[] All = [UkrainianLabel, EnglishLabel, SwitcherName];
    }

    /// <summary>What each language calls itself (Story scope «УКР / ENG»).</summary>
    public const string UkrainianSelfName = "УКР";

    public const string EnglishSelfName = "ENG";

    /// <summary>The <c>lang</c> attribute of the page's <c>html</c> element: the culture it was rendered in.</summary>
    public static string? PageLanguage(string html)
    {
        var match = HtmlLang().Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>True when the page renders a form posting to the language-choice action (spec FR-002).</summary>
    public static bool HasSwitcher(string html) =>
        html.Contains($"action=\"{ChoosePath}\"", StringComparison.Ordinal);

    /// <summary>
    /// The language codes the switcher offers as a submit (spec FR-002: the current one is marked, not a submit).
    /// Each offered language is one form carrying the code as an <c>input name="language"</c>, as the openapi
    /// <c>ChooseUiLanguageForm</c> names it.
    /// </summary>
    public static IReadOnlyList<string> OfferedLanguages(string html) =>
        InputTag().Matches(html)
            .Select(m => Html.InputValue(m.Value, Fields.Language))
            .OfType<string>()
            .ToList();

    /// <summary>The return path the first switcher form carries, decoded; null when absent.</summary>
    public static string? RenderedReturnPath(string html) => Html.InputValue(html, Fields.ReturnPath);

    [GeneratedRegex("<html[^>]*\\slang=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlLang();

    [GeneratedRegex(@"<input\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputTag();
}
