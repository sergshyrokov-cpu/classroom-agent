using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-077 AC-010, AC-011, spec FR-009, I-7: every reference a view makes to its host's own script or stylesheet
/// carries <c>asp-append-version="true"</c>, and only the script and link tag helpers are enabled.
/// </summary>
public sealed partial class VersionedStaticFileRuleTests
{
    private const string VersionAttribute = "asp-append-version=\"true\"";

    private const string Bootstrap = "/lib/bootstrap/css/bootstrap.min.css";

    private const string SiteCss = "/css/site.css";

    private static readonly string[] ExpectedTagHelpers =
    [
        "@addTagHelper Microsoft.AspNetCore.Mvc.TagHelpers.ScriptTagHelper, Microsoft.AspNetCore.Mvc.TagHelpers",
        "@addTagHelper Microsoft.AspNetCore.Mvc.TagHelpers.LinkTagHelper, Microsoft.AspNetCore.Mvc.TagHelpers",
    ];

    public static TheoryData<string> Hosts => new(Styling.Installation, Styling.ControlPlane);

    public static TheoryData<string, string, string> References => new()
    {
        { Styling.Installation, "Shared/_Layout.cshtml", Bootstrap },
        { Styling.Installation, "Shared/_Layout.cshtml", SiteCss },
        { Styling.Installation, "ConnectionInstruction/Index.cshtml", "copy-instruction.js" },
        { Styling.Installation, "Courses/Index.cshtml", "picker-narrow.js" },
        { Styling.Installation, "Journal/Index.cshtml", "picker-narrow.js" },
        { Styling.Installation, "MeetCodes/CourseChoice.cshtml", "picker-narrow.js" },
        { Styling.Installation, "MeetReports/Index.cshtml", "picker-narrow.js" },
        { Styling.Installation, "Report/Index.cshtml", "picker-narrow.js" },
        { Styling.Installation, "MeetReports/Index.cshtml", "meet-export.js" },
        { Styling.Installation, "MeetReports/Meeting.cshtml", "meet-export.js" },
        { Styling.Installation, "Report/Index.cshtml", "report-export.js" },
        { Styling.Installation, "ReportTemplates/Form.cshtml", "report-template-scale.js" },
        { Styling.Installation, "DeanAccounts/Index.cshtml", "password-toggle.js" },
        { Styling.Installation, "DeanPassword/Forced.cshtml", "password-toggle.js" },
        { Styling.Installation, "DeanPassword/Own.cshtml", "password-toggle.js" },
        { Styling.Installation, "SignIn/Index.cshtml", "password-toggle.js" },
        { Styling.ControlPlane, "Shared/_Layout.cshtml", Bootstrap },
        { Styling.ControlPlane, "Shared/_Layout.cshtml", SiteCss },
        { Styling.ControlPlane, "Installations/Detail.cshtml", "copy-identifier.js" },
        { Styling.ControlPlane, "Setup/Index.cshtml", "password-toggle.js" },
        { Styling.ControlPlane, "SignIn/Index.cshtml", "password-toggle.js" },
    };

    /// <summary>AC-010, FR-009: each listed reference in a view is a tag helper tag with the content version switched on.</summary>
    [Theory]
    [MemberData(nameof(References))]
    public void TheListedReference_CarriesTheContentVersion(string host, string view, string file)
    {
        var path = Styling.ProjectFile(host, "Views/" + view);
        var text = Styling.WithoutViewComments(Styling.ReadRequired(path));

        var tags = ScriptOrLinkTag().Matches(text)
            .Select(m => m.Value)
            .Where(tag => (Styling.Attribute(tag, "src") ?? Styling.Attribute(tag, "href") ?? string.Empty).Contains(file, StringComparison.Ordinal))
            .ToList();

        Assert.True(tags.Count > 0, $"{host}/Views/{view}: no <script> or <link> tag refers to {file}.");
        Assert.All(tags, tag => Assert.Contains(VersionAttribute, tag, StringComparison.Ordinal));
    }

    /// <summary>AC-010, FR-009: no reference to the host's own scripts or stylesheets is left without the content version.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryReferenceToTheHostsOwnFiles_CarriesTheContentVersion(string host)
    {
        var root = StaticFiles.RepositoryRoot();
        var offenders = new List<string>();
        var total = 0;
        foreach (var view in InlineCode.ViewsOf(host))
        {
            var text = Styling.WithoutViewComments(File.ReadAllText(Path.Combine(root, view)));
            foreach (Match match in ScriptOrLinkTag().Matches(text))
            {
                var tag = match.Value;
                var address = IsScript(tag) ? Styling.Attribute(tag, "src") : IsStylesheet(tag) ? Styling.Attribute(tag, "href") : null;
                if (address is null || IsForeign(address))
                {
                    continue;
                }

                total++;
                if (!tag.Contains(VersionAttribute, StringComparison.Ordinal))
                {
                    offenders.Add($"{view}: {Regex.Replace(tag, @"\s+", " ")}");
                }
            }
        }

        Assert.Empty(offenders);
        var minimum = host == Styling.Installation ? 16 : 5;
        Assert.True(total >= minimum, $"Only {total} own references found in src/{host}; expected at least {minimum}.");
    }

    /// <summary>AC-011, FR-009, I-7: the view imports enable exactly the script and link tag helpers, never a wildcard.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheViewImports_EnableOnlyTheTwoTagHelpers(string host)
    {
        var lines = Styling.ReadRequired(Styling.ProjectFile(host, "Views/_ViewImports.cshtml"))
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("@addTagHelper", StringComparison.Ordinal))
            .Select(l => Regex.Replace(l, @"\s+", " "))
            .ToList();

        Assert.Equal(ExpectedTagHelpers.Order(StringComparer.Ordinal), lines.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains('*', StringComparison.Ordinal));
    }

    /// <summary>AC-011, I-7: only the view imports enable tag helpers; no view adds its own.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoView_AddsTagHelpersOfItsOwn(string host)
    {
        var root = StaticFiles.RepositoryRoot();
        var imports = "src/" + host + "/Views/_ViewImports.cshtml";
        var offenders = InlineCode.ViewsOf(host)
            .Where(v => !string.Equals(v, imports, StringComparison.Ordinal))
            .Where(v => File.ReadAllText(Path.Combine(root, v)).Contains("@addTagHelper", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsScript(string tag) => tag.StartsWith("<script", StringComparison.OrdinalIgnoreCase);

    private static bool IsStylesheet(string tag) =>
        tag.StartsWith("<link", StringComparison.OrdinalIgnoreCase)
        && (Styling.Attribute(tag, "rel") ?? string.Empty).Contains("stylesheet", StringComparison.OrdinalIgnoreCase);

    private static bool IsForeign(string address) =>
        address.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
        || address.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
        || address.StartsWith("//", StringComparison.Ordinal);

    [GeneratedRegex(@"<(?:script|link)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrLinkTag();
}
