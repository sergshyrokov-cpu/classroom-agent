using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-070 FR-009 items 1, 2, 5, 6, 8 (AC-009, AC-010), api-design <c>x-picker-contract</c>: the narrowing script is read as text, since no
/// browser runs in the test suite. It must exist as one own static file, handle the apostrophe forms and case, split the
/// typed text into words, remove rows rather than style them, set the field's attributes, announce the result politely,
/// keep Enter from submitting, hide an empty block and disable the submit; it reads only the contracted data attributes,
/// sends and stores nothing, runs no dynamic code and assigns no <c>name</c>.
/// </summary>
public sealed partial class PickerNarrowScriptTests
{
    public static TheoryData<string, string> DataAttributes => new()
    {
        { "data-narrow", "narrow" },
        { "data-narrow-label", "narrowLabel" },
        { "data-narrow-none", "narrowNone" },
        { "data-narrow-row", "narrowRow" },
        { "data-narrow-text", "narrowText" },
    };

    public static TheoryData<string> ForbiddenCalls => new(
        "fetch(", "XMLHttpRequest", "localStorage", "sessionStorage", "sendBeacon", "document.cookie", "eval(", "innerHTML");

    private static string ScriptPath() =>
        Path.Combine(StaticFiles.RepositoryRoot(), "src", "ClassroomAgent.Web", "wwwroot", "js", "picker-narrow.js");

    private static string Script() => File.ReadAllText(ScriptPath());

    [GeneratedRegex(@"\\u2019|\u2019", RegexOptions.IgnoreCase)]
    private static partial Regex RightQuote();

    [GeneratedRegex(@"\\u02bc|\u02bc", RegexOptions.IgnoreCase)]
    private static partial Regex ModifierApostrophe();

    [GeneratedRegex(@"\.style\b|setAttribute\(\s*['""]style['""]", RegexOptions.IgnoreCase)]
    private static partial Regex StyleUse();

    [GeneratedRegex(@"\.name\s*=(?!=)|setAttribute\(\s*['""]name['""]", RegexOptions.IgnoreCase)]
    private static partial Regex NameAssignment();

    [GeneratedRegex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>(?<code>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineScript();

    /// <summary>The script with its comments removed, so a word in a comment proves nothing (US-071 AC-013).</summary>
    private static string Code(string source) => LineComment().Replace(BlockComment().Replace(source, " "), " ");

    /// <summary>US-070 FR-009 item 5: a block left with no row gets the <c>hidden</c> attribute, in code.</summary>
    private static bool HidesAnEmptyBlock(string source) => HiddenAssignment().IsMatch(Code(source));

    /// <summary>US-070 FR-009 item 8: the submit's <c>disabled</c> attribute is set, in code.</summary>
    private static bool DisablesTheSubmit(string source) => DisabledAssignment().IsMatch(Code(source));

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"(?m)^\s*//.*$|\s//\s.*$")]
    private static partial Regex LineComment();

    [GeneratedRegex(@"\.hidden\s*=(?!=)")]
    private static partial Regex HiddenAssignment();

    [GeneratedRegex(@"\.disabled\s*=(?!=)")]
    private static partial Regex DisabledAssignment();

    /// <summary>FR-009: the script is one own static file.</summary>
    [Fact]
    public void TheScript_Exists() => Assert.True(File.Exists(ScriptPath()), "wwwroot/js/picker-narrow.js is missing.");

    /// <summary>FR-009 item 2: the typographic apostrophe U+2019 and the modifier letter U+02BC are handled.</summary>
    [Fact]
    public void TheScript_HandlesBothApostropheForms()
    {
        var source = Script();

        Assert.Matches(RightQuote(), source);
        Assert.Matches(ModifierApostrophe(), source);
    }

    /// <summary>FR-009 item 2: case is ignored.</summary>
    [Fact]
    public void TheScript_IgnoresCase()
    {
        var source = Script();

        Assert.True(
            source.Contains(".toLocale", StringComparison.Ordinal) || source.Contains(".toLowerCase", StringComparison.Ordinal),
            "The script folds no case.");
    }

    /// <summary>FR-009 item 2: the typed text is split into words on white space.</summary>
    [Fact]
    public void TheScript_SplitsTheTextOnWhiteSpace() => Assert.Contains(@"/\s+/", Script(), StringComparison.Ordinal);

    /// <summary>FR-009 item 2, SC-15: rows are removed from the page, not styled away (the CSP forbids inline style).</summary>
    [Fact]
    public void TheScript_RemovesRows_AndNeverStylesThem()
    {
        var source = Script();

        Assert.True(
            source.Contains(".remove(", StringComparison.Ordinal) || source.Contains("removeChild(", StringComparison.Ordinal),
            "The script removes no row.");
        Assert.DoesNotMatch(StyleUse(), source);
    }

    /// <summary>FR-009 item 1: the field it adds is not remembered by the browser (x-picker-contract: and is length-limited).</summary>
    [Fact]
    public void TheScript_SetsAutocompleteAndMaxLength()
    {
        var source = Script();

        Assert.Contains("autocomplete", source, StringComparison.Ordinal);
        Assert.True(
            source.Contains("maxlength", StringComparison.Ordinal) || source.Contains("maxLength", StringComparison.Ordinal),
            "The script sets no length limit.");
    }

    /// <summary>FR-009 item 6: the result is announced politely.</summary>
    [Fact]
    public void TheScript_AnnouncesThroughAPoliteLiveRegion()
    {
        var source = Script();

        Assert.Contains("aria-live", source, StringComparison.Ordinal);
        Assert.Contains("polite", source, StringComparison.Ordinal);
    }

    /// <summary>FR-009 item 6: Enter in the field does not submit the form.</summary>
    [Fact]
    public void TheScript_StopsEnterFromSubmitting() => Assert.Contains("preventDefault", Script(), StringComparison.Ordinal);

    /// <summary>FR-009 item 5: a block that holds no row is hidden, by code, not by a word in a comment.</summary>
    [Fact]
    public void TheScript_HidesAnEmptyBlock() => Assert.True(HidesAnEmptyBlock(Script()), "The script hides no block.");

    /// <summary>US-071 AC-013: with the line removed the check fails, though the comment above the function still says "hidden".</summary>
    [Fact]
    public void WithoutTheLine_TheEmptyBlockCheckFails()
    {
        const string Line = "block.element.hidden = shown === 0;";
        var script = Script();
        Assert.Contains(Line, script, StringComparison.Ordinal);
        var without = script.Replace(Line, string.Empty, StringComparison.Ordinal);

        Assert.Contains("hidden", without, StringComparison.Ordinal);
        Assert.False(HidesAnEmptyBlock(without));
    }

    /// <summary>FR-009 item 8: the submit is disabled while no choice remains, by code, not by a word in a comment.</summary>
    [Fact]
    public void TheScript_DisablesTheSubmit() => Assert.True(DisablesTheSubmit(Script()), "The script disables nothing.");

    /// <summary>US-071 AC-013: with the line removed the check fails, though the comment above the function still says "disabled".</summary>
    [Fact]
    public void WithoutTheLine_TheSubmitCheckFails()
    {
        const string Line = "submit.disabled = left === 0;";
        var script = Script();
        Assert.Contains(Line, script, StringComparison.Ordinal);
        var without = script.Replace(Line, string.Empty, StringComparison.Ordinal);

        Assert.Contains("disabled", without, StringComparison.Ordinal);
        Assert.False(DisablesTheSubmit(without));
    }

    /// <summary>x-picker-contract: the script reads the contracted attributes, by attribute name or by dataset name.</summary>
    [Theory]
    [MemberData(nameof(DataAttributes))]
    public void TheScript_ReadsTheContractedAttribute(string attribute, string datasetName)
    {
        var source = Script();

        Assert.True(
            source.Contains(attribute, StringComparison.Ordinal)
            || Regex.IsMatch(source, @"dataset\.\s*" + datasetName + @"\b|dataset\[\s*['""]" + datasetName + @"['""]\s*\]"),
            $"The script reads neither {attribute} nor dataset.{datasetName}.");
    }

    /// <summary>FR-009 item 1, SC-13, SC-15: nothing leaves the page, nothing is stored, no dynamic code is run.</summary>
    [Theory]
    [MemberData(nameof(ForbiddenCalls))]
    public void TheScript_NeverUses(string forbidden) =>
        Assert.DoesNotContain(forbidden, Script(), StringComparison.Ordinal);

    /// <summary>x-picker-contract: the script never assigns a <c>name</c>, so the narrowing field is never submitted.</summary>
    [Fact]
    public void TheScript_NeverAssignsAName() => Assert.DoesNotMatch(NameAssignment(), Script());

    /// <summary>x-picker-contract, SC-15: no view holds inline code about the narrowing; the script is loaded by its tag only.</summary>
    [Fact]
    public void NoView_HoldsInlineNarrowingCode()
    {
        var views = Path.Combine(StaticFiles.RepositoryRoot(), "src", "ClassroomAgent.Web", "Views");
        var files = Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var inline = InlineScript().Matches(File.ReadAllText(file)).Select(m => m.Groups["code"].Value);
            Assert.All(
                inline,
                code => Assert.DoesNotContain("narrow", code, StringComparison.OrdinalIgnoreCase));
        }
    }
}
