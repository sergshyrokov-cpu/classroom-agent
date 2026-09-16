using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Architecture;

/// <summary>AC-002, S-14: the Control Plane references no installation project (AD-1, AD-3, SC-12).</summary>
public sealed class ControlPlaneReferenceTests
{
    private static readonly string[] Forbidden =
    [
        "ClassroomAgent.Domain",
        "ClassroomAgent.Application",
        "ClassroomAgent.Infrastructure",
        "ClassroomAgent.Web",
    ];

    [Fact]
    public void ControlPlane_DoesNotReferenceDomainOrApplication()
    {
        var assemblyReferences = typeof(Program).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);
        var project = XDocument.Load(Path.Combine(
            StaticFiles.RepositoryRoot(),
            "src",
            "ClassroomAgent.ControlPlane",
            "ClassroomAgent.ControlPlane.csproj"));
        var projectReferences = project.Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension((string?)r.Attribute("Include") ?? string.Empty));

        Assert.Equal("ClassroomAgent.ControlPlane", typeof(Program).Assembly.GetName().Name);
        Assert.Empty(assemblyReferences.Intersect(Forbidden, StringComparer.Ordinal));
        Assert.Empty(projectReferences.Intersect(Forbidden, StringComparer.Ordinal));
    }
}
