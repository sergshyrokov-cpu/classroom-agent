using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.PasswordFieldMarkup;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-069 AC-005, AC-006, AC-007, AC-008, read from the source files of both hosts: the script does what spec FR-006
/// says and nothing else, the six views and only they load it, one partial per host holds the control's markup, and
/// <c>site.css</c> keeps the rules of FR-006.1 and FR-008. No test watches a listener run: the project has no tool
/// that could, so SECURITY_REVIEW reads the script too (spec "How the browser behaviour is proved").
/// </summary>
public sealed partial class PasswordToggleSourceTests
{
    public static TheoryData<string> Hosts => new(Styling.Installation, Styling.ControlPlane);

    private static string Script(string host) => Styling.ReadRequired(Styling.FileOf(host, ScriptPath));

    private static string SiteCss(string host) => Styling.ReadRequired(Styling.FileOf(host, Styling.SiteCssPath));

    private static IReadOnlyList<CssDeclaration> ControlRules(string host) =>
        Styling.Declarations(SiteCss(host))
            .Where(d => d.Selector.Contains(ControlClass, StringComparison.Ordinal))
            .ToList();

    // ---- The script ----------------------------------------------------------------------------------------------

    /// <summary>
    /// AC-005, FR-005, FR-006.2: it handles any number of fields by class, marks the field ready, and switches the input's
    /// type, <c>aria-pressed</c> and <c>aria-label</c>. When the control appears is US-073's rule: see
    /// <see cref="PasswordToggleStateSourceTests"/>, which replaced the check that it is revealed at load.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheScript_TogglesTheFieldAndTheControlsState(string host)
    {
        var code = ScriptCode(Script(host));

        foreach (var expected in new[]
        {
            "querySelectorAll", "." + WrapperClass, "." + ControlClass, ReadyClass, "aria-pressed", "aria-label", "\"text\"", "\"password\"",
        })
        {
            Assert.True(
                code.Contains(expected, StringComparison.Ordinal) || code.Contains(expected.Replace('"', '\''), StringComparison.Ordinal),
                $"{host}: the script does not mention {expected}.");
        }

        Assert.DoesNotContain("getElementById", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC-006, FR-006.4: a <c>submit</c> listener on the document or window covers every form on the page, the language
    /// switcher included, and a <c>pagehide</c> listener on the window covers leaving with no form at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheScript_HidesTheFields_OnSubmitAndOnPageHide(string host)
    {
        var script = Script(host);

        Assert.True(ListensForSubmitOnThePage(script), $"{host}: no submit listener on the document.");
        Assert.True(ListensForPageHide(script), $"{host}: no pagehide listener on the window.");
    }

    /// <summary>
    /// AC-005, AC-008, FR-006.2, FR-006.5, SC-13, US-073 AC-007, FR-008: it uses the value only in the one comparison of
    /// the one helper and never changes it, reaches no network, uses no storage or console, cancels no event, submits
    /// nothing and loads nothing from anywhere.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheScript_DoesNothingElse(string host) =>
        Assert.Empty(ScriptProblems(Script(host)));

    /// <summary>The scan above is not vacuous.</summary>
    [Theory]
    [InlineData("var v = input.value;")]
    [InlineData("input.value = '';")]
    [InlineData("input.value.length;")]
    [InlineData("input['value'];")]
    [InlineData("if (input.value === '') { hide(); }")]
    [InlineData("function isEmpty(input) { return input.value.length === 0; }")]
    [InlineData("function isEmpty(input) { return input.value == ''; }")]
    [InlineData("function isEmpty(input) { return input.value === 'x'; }")]
    [InlineData("function isEmpty(input) { return input.value === ''; } function isEmpty(input) { return input.value === ''; }")]
    [InlineData("event.preventDefault();")]
    [InlineData("input.defaultValue = '';")]
    [InlineData("input.getAttribute('value');")]
    [InlineData("fetch('/x');")]
    [InlineData("new XMLHttpRequest();")]
    [InlineData("navigator.sendBeacon('/x');")]
    [InlineData("localStorage.setItem('a', 'b');")]
    [InlineData("sessionStorage.getItem('a');")]
    [InlineData("console.log('x');")]
    [InlineData("form.submit();")]
    [InlineData("el.src = 'a.js';")]
    [InlineData("var u = 'https://cdn.example.test/a.js';")]
    [InlineData("// see https://example.test/page")]
    public void TheScriptScan_ReportsAForbiddenConstruct(string script) =>
        Assert.NotEmpty(ScriptProblems(script));

    /// <summary>The scan accepts what the Specification asks for, and comments are not code.</summary>
    [Theory]
    [InlineData("input.type = 'text';")]
    [InlineData("function isEmpty(input) { return input.value === ''; }")]
    [InlineData("function isEmpty(input) {\n    return input.value === \"\";\n}")]
    [InlineData("control.setAttribute('aria-label', control.dataset.hideName);")]
    [InlineData("// the value is never read: no .value and no console.log here")]
    [InlineData("/* no fetch( and no localStorage */")]
    public void TheScriptScan_AcceptsAnAllowedConstruct(string script) =>
        Assert.Empty(ScriptProblems(script));

    /// <summary>The listener checks are not vacuous: a listener on one form, or a pagehide on the document (which never fires there), does not count.</summary>
    [Theory]
    [InlineData("document.addEventListener('submit', hideAll);", true)]
    [InlineData("window.addEventListener(\"submit\", hideAll);", true)]
    [InlineData("form.addEventListener('submit', hideAll);", false)]
    [InlineData("// document.addEventListener('submit', hideAll);", false)]
    public void TheSubmitCheck_NeedsAPageWideListener(string script, bool expected) =>
        Assert.Equal(expected, ListensForSubmitOnThePage(script));

    [Theory]
    [InlineData("window.addEventListener('pagehide', hideAll);", true)]
    [InlineData("document.addEventListener('pagehide', hideAll);", false)]
    [InlineData("/* window.addEventListener('pagehide', hideAll); */", false)]
    public void ThePageHideCheck_NeedsAWindowListener(string script, bool expected) =>
        Assert.Equal(expected, ListensForPageHide(script));

    // ---- The views -----------------------------------------------------------------------------------------------

    /// <summary>FR-005.5: exactly the six views that hold a password field reference the script.</summary>
    [Theory]
    [InlineData(Styling.Installation, new[] { "DeanAccounts/Index", "DeanPassword/Forced", "DeanPassword/Own", "SignIn/Index" })]
    [InlineData(Styling.ControlPlane, new[] { "Setup/Index", "SignIn/Index" })]
    public void OnlyTheViewsWithAPasswordField_ReferenceTheScript(string host, string[] views)
    {
        var root = StaticFiles.RepositoryRoot();

        var referencing = InlineCode.ViewsOf(host)
            .Where(v => Styling.WithoutViewComments(File.ReadAllText(Path.Combine(root, v))).Contains(ScriptPath, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(views.Select(v => $"src/{host}/Views/{v}.cshtml").Order(StringComparer.Ordinal), referencing);
    }

    /// <summary>FR-005: one partial per host holds the control's markup, so it has one source.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void OnePartial_HoldsTheControlsMarkup(string host)
    {
        var root = StaticFiles.RepositoryRoot();

        var holders = InlineCode.ViewsOf(host)
            .Where(v => ControlClassAttribute().IsMatch(Styling.WithoutViewComments(File.ReadAllText(Path.Combine(root, v)))))
            .ToList();

        var partial = Assert.Single(holders);
        Assert.StartsWith("_", Path.GetFileName(partial), StringComparison.Ordinal);
    }

    // ---- The stylesheet ------------------------------------------------------------------------------------------

    /// <summary>
    /// FR-006.1, AC-007: <c>[hidden]</c> must really hide the control. Bootstrap's rule is <c>display:none!important</c>, so
    /// only an important <c>display</c> or a <c>visibility</c>/<c>opacity</c> trick on the control could defeat it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoRule_DefeatsTheHiddenAttribute(string host)
    {
        Assert.Contains("[hidden]{display:none!important}", Styling.ReadRequired(Styling.FileOf(host, Styling.BootstrapPath)), StringComparison.Ordinal);
        var rules = ControlRules(host);
        Assert.NotEmpty(rules);

        var offenders = rules
            .Where(d => (d.Property == "display" && d.Value.Contains("!important", StringComparison.OrdinalIgnoreCase))
                || d.Property == "visibility")
            .Select(d => $"{d.Selector} {{ {d.Property}: {d.Value} }}")
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// FR-007, FR-008.2, AC-007, AC-011: Edge's own reveal button is hidden only on a ready field, so with scripts off the
    /// browser's own stays and a ready field never shows two.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void EdgesRevealButton_IsHiddenOnlyOnAReadyField(string host)
    {
        var rules = Styling.Declarations(SiteCss(host))
            .Where(d => d.Selector.Contains("::-ms-reveal", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(rules);
        Assert.All(rules, d => Assert.Contains("." + ReadyClass, d.Selector, StringComparison.Ordinal));
        Assert.Contains(rules, d => d.Property == "display" && d.Value.StartsWith("none", StringComparison.Ordinal));
    }

    /// <summary>FR-008.1: icon and state cannot disagree — the background image is selected by <c>aria-pressed</c>, from the two same-host files.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheIcon_IsSelectedByTheControlsState(string host)
    {
        var images = ControlRules(host)
            .Where(d => d.Property is "background-image" or "background")
            .Select(d => (d.Selector, Url: Styling.RootRelativeUrl(d.Value)))
            .Where(i => i.Url is not null)
            .ToList();

        var pressed = images.Where(i => i.Selector.Contains("[aria-pressed=true]", StringComparison.Ordinal)).Select(i => i.Url).Distinct().ToList();
        var notPressed = images.Where(i => !i.Selector.Contains("[aria-pressed=true]", StringComparison.Ordinal)).Select(i => i.Url).Distinct().ToList();

        var shown = Assert.Single(pressed);
        var hidden = Assert.Single(notPressed);
        Assert.NotEqual(shown, hidden);
        Assert.Equal(new[] { EyeOffIconPath, EyeIconPath }.Order(StringComparer.Ordinal), new[] { shown!, hidden! }.Order(StringComparer.Ordinal));
        Assert.True(File.Exists(Styling.FileOf(host, EyeIconPath)), $"{host}: {EyeIconPath} is missing.");
        Assert.True(File.Exists(Styling.FileOf(host, EyeOffIconPath)), $"{host}: {EyeOffIconPath} is missing.");
    }

    /// <summary>FR-008.3, AC-011: a ready field makes room on the right, so the control covers no text (how much is the manual check's).</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void AReadyField_MakesRoomForTheControl(string host) =>
        Assert.Contains(
            Styling.Declarations(SiteCss(host)),
            d => d.Selector.Contains("." + ReadyClass, StringComparison.Ordinal)
                && d.Property is "padding-right" or "padding-inline-end" or "padding");

    /// <summary>FR-008.3: the control's rules are in the part of <c>site.css</c> shared word for word, which is still word for word.</summary>
    [Fact]
    public void TheControlsRules_AreInTheSharedPart_OfBothStylesheets()
    {
        var installation = SharedPart(SiteCss(Styling.Installation));
        var controlPlane = SharedPart(SiteCss(Styling.ControlPlane));

        Assert.Equal(installation, controlPlane);
        Assert.Contains("." + ControlClass, installation, StringComparison.Ordinal);
        Assert.Contains("." + ReadyClass, installation, StringComparison.Ordinal);
        Assert.DoesNotContain(ControlClass, Rest(SiteCss(Styling.Installation)), StringComparison.Ordinal);
        Assert.DoesNotContain(ControlClass, Rest(SiteCss(Styling.ControlPlane)), StringComparison.Ordinal);
    }

    private static string SharedPart(string css)
    {
        var start = css.IndexOf("/* ---- Shared:", StringComparison.Ordinal);
        Assert.True(start >= 0, "site.css has no shared part.");
        var end = FirstHostHeading().Match(css);
        Assert.True(end.Success, "site.css has no host part.");
        return css[start..end.Index].ReplaceLineEndings("\n").Trim();
    }

    private static string Rest(string css) => css[FirstHostHeading().Match(css).Index..];

    [GeneratedRegex(@"\sclass\s*=\s*""[^""]*\bpassword-toggle\b", RegexOptions.IgnoreCase)]
    private static partial Regex ControlClassAttribute();

    [GeneratedRegex(@"/\* ---- (?:Installation|Control Plane):")]
    private static partial Regex FirstHostHeading();
}
