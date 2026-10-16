using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-050 FR-001, SC-15 "one host-wide rule": outside the host's <c>Security</c> namespace no source file names one of
/// the four headers, and no view sets one through <c>&lt;meta http-equiv&gt;</c>. The behaviour of the rule is proven
/// by the <c>SecurityHeadersResponseTests</c> of each host; this guards the other half — that no per-page decision
/// exists beside it. Passes before implementation (nothing names the headers today).
/// </summary>
public sealed class SecurityHeadersRuleTests
{
    /// <summary>
    /// Header names in their string and identifier spellings. <c>SuppressXFrameOptionsHeader</c> is the antiforgery
    /// option spec FR-005 allows and is removed before the scan.
    /// </summary>
    private static readonly string[] HeaderTokens =
    [
        "X-Frame-Options", "XFrameOptions",
        "X-Content-Type-Options", "XContentTypeOptions",
        "Content-Security-Policy", "ContentSecurityPolicy",
        "Referrer-Policy", "ReferrerPolicy",
    ];

    public static TheoryData<string> Hosts => new("ClassroomAgent.Web", "ClassroomAgent.ControlPlane");

    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoSourceFileOutsideTheSecurityNamespace_NamesASecurityHeader(string host)
    {
        var projectDirectory = Path.Combine(StaticFiles.RepositoryRoot(), "src", host);
        var security = Path.Combine(projectDirectory, "Security") + Path.DirectorySeparatorChar;

        var offenders = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(security, StringComparison.OrdinalIgnoreCase))
            .Where(f => !IsBuildOutput(Path.GetRelativePath(projectDirectory, f)))
            .SelectMany(f =>
            {
                var text = File.ReadAllText(f).Replace("SuppressXFrameOptionsHeader", string.Empty, StringComparison.Ordinal);
                return HeaderTokens
                    .Where(t => text.Contains(t, StringComparison.OrdinalIgnoreCase))
                    .Select(t => $"{Path.GetRelativePath(projectDirectory, f)}: {t}");
            })
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoView_SetsAHeaderThroughMeta(string host)
    {
        var root = StaticFiles.RepositoryRoot();

        var offenders = InlineCode.ViewsOf(host)
            .Where(v => File.ReadAllText(Path.Combine(root, v)).Contains("http-equiv", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsBuildOutput(string relativePath) =>
        relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
