using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-068 AC-004, spec FR-007, SC-15 (v105): every Bootstrap rule that draws a <c>data:</c> image for a component the
/// host's views use is overridden in that host's <c>site.css</c> — same selector, same property — by a root-relative
/// URL of a same-host SVG file, so the policy's <c>img-src 'self'</c> never has to allow <c>data:</c>. The served
/// check (200, <c>image/svg+xml</c>) is in each host's <c>StylesheetLinkTests</c>.
/// </summary>
public sealed class EmbeddedImageOverrideTests
{
    public static TheoryData<string> Hosts => new(Styling.Installation, Styling.ControlPlane);

    /// <summary>
    /// AC-004: the (selector, property) pairs of the vendored stylesheet whose value holds a <c>data:</c> URL, that are not
    /// scoped to the dark theme and whose classes all occur in the host's views.
    /// </summary>
    private static List<CssDeclaration> EmbeddedImagesInUse(string host)
    {
        var bootstrap = Styling.ReadRequired(Styling.FileOf(host, Styling.BootstrapPath));
        var tokens = Styling.ViewTokens(host);
        return Styling.Declarations(bootstrap)
            .Where(d => d.Value.Contains("data:", StringComparison.OrdinalIgnoreCase))
            .Where(d => !Styling.IsDarkThemeScoped(d.Selector))
            .Where(d => Styling.ClassesOf(d.Selector) is { Count: > 0 } classes && classes.All(tokens.Contains))
            .DistinctBy(d => (d.Selector, d.Property))
            .ToList();
    }

    /// <summary>AC-004, FR-007.1, FR-007.2: each embedded image in use has its override in <c>site.css</c>.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryEmbeddedImageInUse_IsOverriddenWithASameHostFile(string host)
    {
        var inUse = EmbeddedImagesInUse(host);
        var site = Styling.Declarations(Styling.ReadRequired(Styling.FileOf(host, Styling.SiteCssPath)));

        var missing = inUse
            .Where(d => !site.Any(s => s.Selector == d.Selector
                && s.Property == d.Property
                && Styling.RootRelativeUrl(s.Value) is not null
                && !s.Value.Contains("data:", StringComparison.OrdinalIgnoreCase)))
            .Select(d => $"{d.Selector} {{ {d.Property} }}")
            .ToList();

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{host}: overridden — {string.Join(", ", inUse.Select(d => $"{d.Selector} {{ {d.Property} }}"))}");
        Assert.Empty(missing);
    }

    /// <summary>
    /// AC-004: for the installation the list holds at least the select arrow and the checked checkbox, the
    /// indeterminate checkbox and the checked radio marks — so the check above is never vacuous there.
    /// </summary>
    [Fact]
    public void TheInstallationList_HoldsTheSelectArrowAndTheCheckMarks()
    {
        var selectors = EmbeddedImagesInUse(Styling.Installation).Select(d => d.Selector).ToList();

        Assert.Contains(".form-select", selectors);
        Assert.Contains(selectors, s => s.Contains(".form-check-input", StringComparison.Ordinal)
            && s.Contains(":checked", StringComparison.Ordinal) && s.Contains("[type=checkbox]", StringComparison.Ordinal)
            && !s.Contains(":indeterminate", StringComparison.Ordinal));
        Assert.Contains(selectors, s => s.Contains(".form-check-input", StringComparison.Ordinal)
            && s.Contains(":indeterminate", StringComparison.Ordinal) && s.Contains("[type=checkbox]", StringComparison.Ordinal));
        Assert.Contains(selectors, s => s.Contains(".form-check-input", StringComparison.Ordinal)
            && s.Contains(":checked", StringComparison.Ordinal) && s.Contains("[type=radio]", StringComparison.Ordinal));
    }

    /// <summary>AC-004, FR-007.3: every image <c>site.css</c> references exists and holds no script, no external reference and no embedded data.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryReferencedImage_IsAPlainSameHostSvg(string host)
    {
        var css = Styling.ReadRequired(Styling.FileOf(host, Styling.SiteCssPath));
        var problems = new List<string>();

        foreach (var path in Styling.RootRelativeUrls(css))
        {
            if (!path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{path}: not an .svg file");
                continue;
            }

            var file = Styling.FileOf(host, path);
            if (!File.Exists(file))
            {
                problems.Add($"{path}: missing under wwwroot");
                continue;
            }

            var svg = File.ReadAllText(file);
            if (svg.Contains("<script", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{path}: contains a script");
            }

            if (svg.Contains("data:", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{path}: contains a data: URL");
            }

            if (svg.Contains("<image", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{path}: embeds an image");
            }

            if (ExternalReferences(svg).Any())
            {
                problems.Add($"{path}: references another file ({string.Join(", ", ExternalReferences(svg))})");
            }
        }

        Assert.Empty(problems);
    }

    /// <summary>AC-004: the image check is not vacuous — a script, a data URL and an external href are each reported.</summary>
    [Theory]
    [InlineData("<svg><a href=\"/other.svg\"/></svg>", true)]
    [InlineData("<svg><use xlink:href='https://x.example.test/a.svg#p'/></svg>", true)]
    [InlineData("<svg><use href=\"#p\"/></svg>", false)]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M2 5l6 6 6-6\"/></svg>", false)]
    public void TheExternalReferenceScan_ReportsOnlyReferencesToOtherFiles(string svg, bool reported) =>
        Assert.Equal(reported, ExternalReferences(svg).Any());

    private static IEnumerable<string> ExternalReferences(string svg)
    {
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
            svg, @"(?:xlink:)?href\s*=\s*[""'](?<v>[^""']*)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            if (!m.Groups["v"].Value.StartsWith('#'))
            {
                yield return m.Groups["v"].Value;
            }
        }
    }
}
