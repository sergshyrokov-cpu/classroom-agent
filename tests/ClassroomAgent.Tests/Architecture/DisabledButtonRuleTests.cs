using System.Globalization;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-071 AC-011, FR-012, I-15, openapi <c>x-page-frame.disabled-buttons</c>: one rule in the shared first part of both hosts'
/// <c>site.css</c> sets Bootstrap's own disabled variables on <c>.btn:disabled</c> to grey with readable text, whatever the
/// button's colour. Colours are read from the rule: a browser is not in the suite, and the person checks the look (AC-011's walk).
/// </summary>
public sealed class DisabledButtonRuleTests
{
    private const string Selector = ".btn:disabled";

    private const string Background = "--bs-btn-disabled-bg";

    private const string Text = "--bs-btn-disabled-color";

    private const string Border = "--bs-btn-disabled-border-color";

    private const string Opacity = "--bs-btn-disabled-opacity";

    /// <summary>Bootstrap 5.3's greys, by the variable a rule may name.</summary>
    private static readonly IReadOnlyDictionary<string, string> Greys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["var(--bs-gray-100)"] = "#f8f9fa",
        ["var(--bs-gray-200)"] = "#e9ecef",
        ["var(--bs-gray-300)"] = "#dee2e6",
        ["var(--bs-gray-400)"] = "#ced4da",
        ["var(--bs-gray-500)"] = "#adb5bd",
        ["var(--bs-gray-600)"] = "#6c757d",
        ["var(--bs-gray-700)"] = "#495057",
        ["var(--bs-gray-800)"] = "#343a40",
        ["var(--bs-gray-900)"] = "#212529",
        ["var(--bs-white)"] = "#ffffff",
        ["var(--bs-black)"] = "#000000",
    };

    public static TheoryData<string> Hosts => new(Styling.Installation, Styling.ControlPlane);

    private static string SiteCss(string host) => File.ReadAllText(Styling.FileOf(host, Styling.SiteCssPath));

    private static string SharedPart(string css)
    {
        var start = css.IndexOf("/* ---- Shared:", StringComparison.Ordinal);
        Assert.True(start >= 0, "site.css has no shared part.");
        var end = css.IndexOf("/* ---- ", css.LastIndexOf("/* ---- Shared:", StringComparison.Ordinal) + 5, StringComparison.Ordinal);
        Assert.True(end > start, "site.css has no host part.");
        return css[start..end];
    }

    private static string? ValueOf(string css, string property) =>
        Styling.Declarations(css)
            .LastOrDefault(d => Styling.NormalizeSelector(d.Selector) == Selector && d.Property == property)?.Value;

    private static string Require(string css, string property)
    {
        var value = ValueOf(css, property);
        Assert.True(value is not null, $"{Selector} in the shared part sets no {property}.");
        return value;
    }

    private static (int R, int G, int B) Colour(string value)
    {
        var hex = Greys.TryGetValue(value.Trim(), out var named) ? named : value.Trim();
        Assert.True(hex.Length == 7 && hex[0] == '#', $"'{value}' is neither a #rrggbb colour nor one of Bootstrap's greys.");
        return (Channel(hex, 1), Channel(hex, 3), Channel(hex, 5));
    }

    private static int Channel(string hex, int at) => int.Parse(hex.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static double Luminance((int R, int G, int B) c)
    {
        static double Linear(int v)
        {
            var s = v / 255d;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
    }

    /// <summary>The colour as shown over a white page with the rule's opacity.</summary>
    private static (int R, int G, int B) OverWhite((int R, int G, int B) c, double opacity) =>
        ((int)Math.Round((c.R * opacity) + (255 * (1 - opacity))),
         (int)Math.Round((c.G * opacity) + (255 * (1 - opacity))),
         (int)Math.Round((c.B * opacity) + (255 * (1 - opacity))));

    /// <summary>Bootstrap's own default when the rule sets none: 0.65.</summary>
    private static double OpacityOf(string css) =>
        ValueOf(css, Opacity) is { } value ? double.Parse(value, CultureInfo.InvariantCulture) : 0.65;

    /// <summary>AC-011: the rule is in the shared part of both hosts' <c>site.css</c>, and sets the three colours.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheRule_IsInTheSharedPart(string host)
    {
        var shared = SharedPart(SiteCss(host));

        Require(shared, Background);
        Require(shared, Text);
        Require(shared, Border);
    }

    /// <summary>NFR-076, US-068 FR-010.1: both hosts carry the rule with the same values.</summary>
    [Fact]
    public void BothHosts_HaveTheSameValues()
    {
        var installation = SharedPart(SiteCss(Styling.Installation));
        var controlPlane = SharedPart(SiteCss(Styling.ControlPlane));
        Require(installation, Background);

        foreach (var property in new[] { Background, Text, Border, Opacity })
        {
            Assert.Equal(ValueOf(installation, property), ValueOf(controlPlane, property));
        }
    }

    /// <summary>
    /// FR-012: the background and the border are grey: no channel stands out from the others by more than Bootstrap's own bluish
    /// greys do (20 of 255), which a paler blue or red would.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheBackgroundAndBorder_AreGrey(string host)
    {
        var shared = SharedPart(SiteCss(host));

        foreach (var property in new[] { Background, Border })
        {
            var (r, g, b) = Colour(Require(shared, property));
            Assert.True(
                Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) <= 20,
                $"{property} of {host} is not grey: {r},{g},{b}.");
        }
    }

    /// <summary>
    /// FR-012 "with readable text": text and background as shown, with the opacity the rule leaves, differ by at least 3:1. The ratio
    /// is this test's reading of "readable" for a control that cannot be used anyway; Bootstrap's default 0.65 over its grey fails it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheText_IsReadable(string host)
    {
        var shared = SharedPart(SiteCss(host));
        var opacity = OpacityOf(shared);
        var background = Luminance(OverWhite(Colour(Require(shared, Background)), opacity));
        var text = Luminance(OverWhite(Colour(Require(shared, Text)), opacity));

        var ratio = (Math.Max(background, text) + 0.05) / (Math.Min(background, text) + 0.05);

        Assert.True(ratio >= 3, $"{host}: contrast {ratio:F2} is under 3:1.");
    }

    /// <summary>
    /// FR-012: "enabled buttons do not change": no rule of <c>site.css</c> sets these variables on a selector that also matches an
    /// enabled button.
    /// </summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoEnabledButton_GetsTheseVariables(string host)
    {
        var others = Styling.Declarations(SiteCss(host))
            .Where(d => d.Property.StartsWith("--bs-btn-disabled", StringComparison.Ordinal))
            .Select(d => Styling.NormalizeSelector(d.Selector))
            .Where(s => s != Selector)
            .Distinct()
            .ToList();

        Assert.Empty(others);
    }
}
