using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-045 AC-004, spec FR-006: the US-021 point fix keyed on one event id is gone; the host-wide rule now carries its
/// guarantee, which <c>CourseCardLoggingTests</c> keep asserting unchanged.
/// </summary>
public sealed class RouteTemplateLoggingRuleTests
{
    private const string PointFix = "RefusedValueRedaction";

    [Fact]
    public void TheRefusedValueRedaction_TypeNoLongerExists()
    {
        var types = typeof(ClassroomAgent.Web.Configuration.InstallationLogging).Assembly.GetTypes();

        Assert.DoesNotContain(types, t => t.Name == PointFix);
    }

    [Fact]
    public void NoProductionSource_NamesTheRefusedValueRedaction()
    {
        var sources = Path.Combine(StaticFiles.RepositoryRoot(), "src");
        var offenders = Directory.EnumerateFiles(sources, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains(PointFix, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(sources, f))
            .ToList();

        Assert.Empty(offenders);
    }
}
