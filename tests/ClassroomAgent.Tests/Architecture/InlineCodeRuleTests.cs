using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-050 AC-005, spec FR-006, VR-003, SC-15: no Razor view of either host carries code a strict policy refuses —
/// no <c>&lt;script&gt;</c> or (SPEC_REVIEW m-1) <c>&lt;style&gt;</c> element with a body, no event-handler
/// attribute, no <c>style</c> attribute. Today every view complies (spec FR-006), so this is a guard: it passes before
/// implementation, and the scanner's own tests below prove it is not vacuous.
/// </summary>
public sealed class InlineCodeRuleTests
{
    public static TheoryData<string> Hosts => new("ClassroomAgent.Web", "ClassroomAgent.ControlPlane");

    /// <summary>AC-005: every <c>.cshtml</c> of the host.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryView_CarriesNoInlineCode(string host)
    {
        var root = StaticFiles.RepositoryRoot();
        var views = InlineCode.ViewsOf(host);
        Assert.True(views.Count > 3, $"Only {views.Count} views found under src/{host}.");

        var violations = views
            .SelectMany(v => InlineCode.Violations(File.ReadAllText(Path.Combine(root, v))).Select(x => $"{v}: {x}"))
            .ToList();

        Assert.Empty(violations);
    }

    /// <summary>AC-005: the scan reaches the views that include scripts, so a regression there would be seen.</summary>
    [Theory]
    [InlineData("ClassroomAgent.Web", "src/ClassroomAgent.Web/Views/ConnectionInstruction/Index.cshtml")]
    [InlineData("ClassroomAgent.Web", "src/ClassroomAgent.Web/Views/Shared/_Layout.cshtml")]
    public void TheScan_ReachesTheViewsThatIncludeScripts(string host, string view) =>
        Assert.Contains(view, InlineCode.ViewsOf(host));

    /// <summary>FR-007, AC-006: no script file of either host relies on what the policy refuses (eval, string timers, inline attributes).</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryScript_RunsUnderTheStrictPolicy(string host)
    {
        var root = StaticFiles.RepositoryRoot();
        var scripts = InlineCode.ScriptsOf(host);
        Assert.NotEmpty(scripts);

        var violations = scripts
            .SelectMany(s => InlineCode.ScriptViolations(File.ReadAllText(Path.Combine(root, s))).Select(x => $"{s}: {x}"))
            .ToList();

        Assert.Empty(violations);
    }

    /// <summary>VR-003: markup the scan must report.</summary>
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<SCRIPT type=\"text/javascript\">\n  var x = 1;\n</SCRIPT>")]
    [InlineData("<button onclick=\"go()\">Go</button>")]
    [InlineData("<form\n    onSubmit = \"return check()\">")]
    [InlineData("<div style=\"color: red\">x</div>")]
    [InlineData("<td STYLE='width:10px'>x</td>")]
    [InlineData("<style>\n  .a { color: red; }\n</style>")]
    public void TheScan_ReportsInlineCode(string markup) =>
        Assert.NotEmpty(InlineCode.Violations(markup));

    /// <summary>VR-003, FR-006: markup the scan must accept.</summary>
    [Theory]
    [InlineData("<script src=\"~/js/copy-instruction.js\" asp-append-version=\"true\"></script>")]
    [InlineData("<script src=\"/js/a.js\">\n</script>")]
    [InlineData("<button type=\"button\" data-copy-target=\"instruction\">Copy</button>")]
    [InlineData("<link rel=\"stylesheet\" href=\"~/css/site.css\" />")]
    [InlineData("<div class=\"online\">x</div>")]
    [InlineData("@* <div style=\"x\"> in a Razor comment *@")]
    [InlineData("<!-- <script>old()</script> -->")]
    public void TheScan_AcceptsCompliantMarkup(string markup) =>
        Assert.Empty(InlineCode.Violations(markup));

    /// <summary>FR-004, FR-007: script constructs the policy refuses.</summary>
    [Theory]
    [InlineData("eval(text);")]
    [InlineData("const f = new Function(\"return 1\");")]
    [InlineData("setTimeout(\"go()\", 10);")]
    [InlineData("el.setAttribute(\"style\", \"color:red\");")]
    [InlineData("el.setAttribute('onclick', 'go()');")]
    public void TheScriptScan_ReportsRefusedConstructs(string script) =>
        Assert.NotEmpty(InlineCode.ScriptViolations(script));

    /// <summary>FR-007: CSSOM and ordinary attributes stay allowed; comments are not code.</summary>
    [Theory]
    [InlineData("input.setAttribute(\"inputmode\", \"numeric\");")]
    [InlineData("el.style.width = \"10px\";")]
    [InlineData("setTimeout(() => go(), 10);")]
    [InlineData("// a policy without 'unsafe-eval' refuses eval(x)")]
    public void TheScriptScan_AcceptsAllowedConstructs(string script) =>
        Assert.Empty(InlineCode.ScriptViolations(script));
}
