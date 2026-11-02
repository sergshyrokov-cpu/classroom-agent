using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-069 AC-002, spec FR-002, read from the source files of both hosts: the layouts are the only views that may hold an
/// <c>&lt;h1&gt;</c>, and every view that is not a partial gives the layout its title.
/// </summary>
public sealed partial class OnePageTitleRuleTests
{
    public static TheoryData<string> Hosts => new(Styling.Installation, Styling.ControlPlane);

    private static string Read(string view) =>
        Styling.WithoutViewComments(File.ReadAllText(Path.Combine(StaticFiles.RepositoryRoot(), view)));

    private static string LayoutOf(string host) => $"src/{host}/Views/Shared/_Layout.cshtml";

    /// <summary>AC-002: no view of either host other than its layout holds an <c>&lt;h1&gt;</c>.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoViewButTheLayout_HoldsAnH1(string host)
    {
        var views = InlineCode.ViewsOf(host);
        Assert.True(views.Count > 3, $"Only {views.Count} views found under src/{host}.");

        var offenders = views
            .Where(v => v != LayoutOf(host))
            .Where(v => H1().IsMatch(Read(v)))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>AC-002: the layout holds exactly one, so the scan above leaves every page with one title.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheLayout_HoldsOneH1(string host) =>
        Assert.Single(H1().Matches(Read(LayoutOf(host))));

    /// <summary>FR-002: every non-partial view sets the title, so no page has an empty one.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryNonPartialView_SetsTheTitle(string host)
    {
        var offenders = InlineCode.ViewsOf(host)
            .Where(v => !Path.GetFileName(v).StartsWith('_'))
            .Where(v => !Read(v).Contains("ViewData[\"Title\"]", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>The scan is not vacuous, and a comment or an <c>h2</c> is not a title.</summary>
    [Theory]
    [InlineData("<h1>@T[\"X\"]</h1>", true)]
    [InlineData("<H1 class=\"a\">x</H1>", true)]
    [InlineData("<h2>x</h2>", false)]
    [InlineData("@* <h1>x</h1> *@", false)]
    [InlineData("<!-- <h1>x</h1> -->", false)]
    public void TheScan_FindsAnH1(string view, bool found) =>
        Assert.Equal(found, H1().IsMatch(Styling.WithoutViewComments(view)));

    [GeneratedRegex(@"<h1\b", RegexOptions.IgnoreCase)]
    private static partial Regex H1();
}
