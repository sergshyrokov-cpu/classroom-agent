using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-069 test support: reads the show-password control out of a rendered page and out of the script's source
/// (spec FR-005, FR-006). The project has no browser test tool, so markup and source are what an automated test can
/// prove (spec "How the browser behaviour is proved").
/// </summary>
public static partial class PasswordFieldMarkup
{
    /// <summary>FR-005.5: the host's script, as a page references it.</summary>
    public const string ScriptPath = "/js/password-toggle.js";

    /// <summary>FR-008.1: the two same-host icons.</summary>
    public const string EyeIconPath = "/img/password/eye.svg";

    public const string EyeOffIconPath = "/img/password/eye-off.svg";

    public const string WrapperClass = "password-field";

    public const string ReadyClass = "password-field--ready";

    public const string ControlClass = "password-toggle";

    /// <summary>FR-005.4, by language.</summary>
    public static string ShowName(string language) => language == "uk" ? "Показати пароль" : "Show password";

    public static string HideName(string language) => language == "uk" ? "Сховати пароль" : "Hide password";

    /// <summary>What a field must keep from today (FR-005.3): its name, its <c>autocomplete</c> and its <c>value</c>; null means the attribute is absent.</summary>
    public sealed record ExpectedField(string Name, string? Autocomplete, string? Value);

    /// <summary>One wrapper with the input first and the control after it (FR-005.1).</summary>
    public sealed record Field(string Wrapper, string Input, string Control, string ControlInner);

    /// <summary>Every <c>&lt;input type="password"&gt;</c> of a page, wrapped or not.</summary>
    public static IReadOnlyList<string> PasswordInputs(string html) =>
        InputTag().Matches(html).Select(m => m.Value).Where(t => Value(t, "type") == "password").ToList();

    /// <summary>Every <c>span.password-field</c> holding an input and then a button, in page order.</summary>
    public static IReadOnlyList<Field> Fields(string html) =>
        WrapperBlock().Matches(html)
            .Where(m => Classes(m.Groups["wrapper"].Value).Contains(WrapperClass, StringComparer.Ordinal))
            .Select(m => new Field(m.Groups["wrapper"].Value, m.Groups["input"].Value, m.Groups["control"].Value, m.Groups["inner"].Value))
            .ToList();

    /// <summary>The decoded value of an attribute of one tag; null when absent.</summary>
    public static string? Value(string tag, string attribute)
    {
        var raw = Styling.Attribute(tag, attribute);
        return raw is null ? null : WebUtility.HtmlDecode(raw);
    }

    /// <summary>Whether a tag carries the attribute, with or without a value (<c>hidden</c>).</summary>
    public static bool HasAttribute(string tag, string attribute) =>
        Regex.IsMatch(tag, $@"\s{Regex.Escape(attribute)}(?=[\s=/>])", RegexOptions.IgnoreCase);

    public static IReadOnlyList<string> Classes(string tag) =>
        (Value(tag, "class") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The decoded values of a tag's <c>data-*</c> attributes (the Specification does not name them, FR-005.2).</summary>
    public static IReadOnlyList<string> DataValues(string tag) =>
        DataAttribute().Matches(tag).Select(m => WebUtility.HtmlDecode(m.Groups["v"].Value)).ToList();

    /// <summary>FR-005.1, FR-005.2, FR-005.3: one field, in the page's language.</summary>
    public static void AssertField(Field field, ExpectedField expected, string language)
    {
        var where = $"field '{expected.Name}'";

        Assert.True(field.Wrapper.StartsWith("<span", StringComparison.OrdinalIgnoreCase), $"{where}: the wrapper is not a span.");

        Assert.Equal("password", Value(field.Input, "type"));
        Assert.Equal(expected.Name, Value(field.Input, "name"));
        Assert.Equal(expected.Autocomplete, Value(field.Input, "autocomplete"));
        Assert.Equal(expected.Value, Value(field.Input, "value"));
        Assert.Equal("false", Value(field.Input, "spellcheck"));
        Assert.Equal("off", Value(field.Input, "autocapitalize"));

        Assert.Equal("button", Value(field.Control, "type"));
        Assert.True(HasAttribute(field.Control, "hidden"), $"{where}: the control is not rendered hidden.");
        Assert.Contains(ControlClass, Classes(field.Control));
        Assert.Equal("false", Value(field.Control, "aria-pressed"));
        Assert.Equal(ShowName(language), Value(field.Control, "aria-label"));
        Assert.Contains(ShowName(language), DataValues(field.Control));
        Assert.Contains(HideName(language), DataValues(field.Control));

        foreach (var forbidden in new[] { "name", "value", "form", "id" })
        {
            Assert.False(HasAttribute(field.Control, forbidden), $"{where}: the control carries a '{forbidden}' attribute.");
        }

        Assert.True(string.IsNullOrWhiteSpace(field.ControlInner), $"{where}: the control holds visible content: {field.ControlInner}");
    }

    /// <summary>The whole <c>&lt;form&gt;</c> holding the n-th password input of a page.</summary>
    public static IReadOnlyList<string> FormsWithAPasswordField(string html) =>
        FormBlock().Matches(html).Select(m => m.Value).Where(f => PasswordInputs(f).Count > 0).ToList();

    /// <summary>The names a form sends: each named input, select, textarea and button, in order.</summary>
    public static IReadOnlyList<string> NamedControls(string form) =>
        NamedControlTag().Matches(form)
            .Select(m => Value(m.Value, "name"))
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();

    /// <summary>Every <c>&lt;label&gt;</c> block of a page that holds a password field wrapper (FR-005.1).</summary>
    public static IReadOnlyList<string> LabelsHoldingAField(string html) =>
        LabelBlock().Matches(html).Select(m => m.Value).Where(l => Fields(l).Count > 0).ToList();

    /// <summary>Every <c>&lt;script&gt;</c> tag of a page referencing the host's script, query string ignored.</summary>
    public static IReadOnlyList<string> ScriptReferences(string html) =>
        ScriptTag().Matches(html)
            .Select(m => m.Value)
            .Where(t => (Value(t, "src") ?? string.Empty).Split('?')[0] == ScriptPath)
            .ToList();

    // ---- Script source -------------------------------------------------------------------------------------------

    /// <summary>The source text with comments removed; a <c>//</c> after a colon or a quote is kept (it is a URL, not a comment).</summary>
    public static string ScriptCode(string script) =>
        ScriptLineComment().Replace(ScriptBlockComment().Replace(script, " "), " ");

    /// <summary>
    /// FR-006.2, FR-006.5, SC-13: what the script must never do — read or change the value, reach the network, use
    /// browser storage, write to the console, submit a form or load anything. Empty when it complies.
    /// </summary>
    public static IReadOnlyList<string> ScriptProblems(string script)
    {
        var code = ScriptCode(script);
        var problems = ForbiddenConstruct().Matches(code).Select(m => $"forbidden construct: {m.Value}").ToList();
        problems.AddRange(ExternalUrl().Matches(script).Select(m => $"URL of another site: {m.Value}"));
        return problems;
    }

    /// <summary>FR-006.4: a listener for <c>submit</c> on the document or the window, so that every form on the page is covered.</summary>
    public static bool ListensForSubmitOnThePage(string script) => DocumentSubmitListener().IsMatch(ScriptCode(script));

    /// <summary>FR-006.4: a listener for <c>pagehide</c> on the window, the only target the event is dispatched to.</summary>
    public static bool ListensForPageHide(string script) => WindowPageHideListener().IsMatch(ScriptCode(script));

    [GeneratedRegex(@"<input\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputTag();

    [GeneratedRegex(
        @"(?<wrapper><span\b[^>]*>)\s*(?<input><input\b[^>]*>)\s*(?<control><button\b[^>]*>)(?<inner>.*?)</button>\s*</span>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WrapperBlock();

    [GeneratedRegex(@"\sdata-[a-z0-9-]+\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex DataAttribute();

    [GeneratedRegex(@"<form\b.*?</form\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FormBlock();

    [GeneratedRegex(@"<(?:input|select|textarea|button)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex NamedControlTag();

    [GeneratedRegex(@"<label\b.*?</label\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LabelBlock();

    [GeneratedRegex(@"<script\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTag();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex ScriptBlockComment();

    [GeneratedRegex(@"(?<![:""'])//[^\r\n]*")]
    private static partial Regex ScriptLineComment();

    [GeneratedRegex(
        @"\.value\b|\.defaultValue\b|(?:get|set)Attribute\(\s*[""']value[""']|\bfetch\s*\(|XMLHttpRequest|sendBeacon|WebSocket|EventSource"
        + @"|localStorage|sessionStorage|indexedDB|\bcaches\.|document\.cookie|\bconsole\.|FormData|\bimport\s*\(|\.src\b|\.href\b|\.action\b"
        + @"|\.submit\s*\(|requestSubmit|\.innerHTML|\.outerHTML|\.textContent|\blocation\.|document\.write")]
    private static partial Regex ForbiddenConstruct();

    [GeneratedRegex(@"\b(?:https?:)?//[a-z0-9][a-z0-9.-]*\.[a-z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUrl();

    [GeneratedRegex(@"\b(?:document|window)\s*\.\s*addEventListener\(\s*[""']submit[""']")]
    private static partial Regex DocumentSubmitListener();

    [GeneratedRegex(@"\bwindow\s*\.\s*addEventListener\(\s*[""']pagehide[""']")]
    private static partial Regex WindowPageHideListener();
}
