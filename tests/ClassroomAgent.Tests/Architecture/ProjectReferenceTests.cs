using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-005 FR-014, S-03, S-11: the six projects reference each other exactly as <c>package-map.md</c>
/// allows (AD-1, AD-3, AD-4), and the layers that must stay free of frameworks do.
/// </summary>
public sealed class ProjectReferenceTests
{
    public static TheoryData<string, string[]> AllowedReferences => new()
    {
        { "ClassroomAgent.Domain", [] },
        { "ClassroomAgent.Contracts", [] },
        { "ClassroomAgent.Application", ["ClassroomAgent.Domain"] },
        { "ClassroomAgent.Infrastructure", ["ClassroomAgent.Application", "ClassroomAgent.Contracts", "ClassroomAgent.Domain"] },
        { "ClassroomAgent.Web", ["ClassroomAgent.Application", "ClassroomAgent.Contracts", "ClassroomAgent.Domain", "ClassroomAgent.Infrastructure"] },
        { "ClassroomAgent.ControlPlane", ["ClassroomAgent.Contracts"] },
    };

    [Theory]
    [MemberData(nameof(AllowedReferences))]
    public void ProjectReferences_AreExactlyThoseAllowed(string project, string[] allowed)
    {
        var references = ProjectReferences(project);

        Assert.Equal(allowed.Order(StringComparer.Ordinal), references.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("ClassroomAgent.Domain")]
    [InlineData("ClassroomAgent.Contracts")]
    [InlineData("ClassroomAgent.Application")]
    public void FrameworkFreeProjects_ReferenceNoPackage(string project)
    {
        var document = XDocument.Load(ProjectFile(project));

        Assert.Empty(document.Descendants("PackageReference"));
        Assert.Empty(document.Descendants("FrameworkReference"));
        Assert.Equal("Microsoft.NET.Sdk", (string?)document.Root!.Attribute("Sdk"));
    }

    [Fact]
    public void ApplicationAssembly_ReferencesNoInfrastructureContractsOrEfCore()
    {
        var referenced = typeof(ClassroomAgent.Application.Ports.IControlPlaneClient).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.StartsWith("Npgsql", StringComparison.Ordinal));
        Assert.DoesNotContain("ClassroomAgent.Infrastructure", referenced);
        Assert.DoesNotContain("ClassroomAgent.Contracts", referenced);
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void ContractsAssembly_HasOnlyWireTypes_NoTeachingDataName()
    {
        var names = typeof(ClassroomAgent.Contracts.LegitimacyCheckRequest).Assembly.GetExportedTypes()
            .Select(t => t.Name)
            .ToList();
        string[] teachingData = ["Course", "Participant", "Submission", "Grade", "Meet", "Journal", "Student", "Teacher"];

        Assert.Contains("LegitimacyCheckRequest", names);
        Assert.Contains("LegitimacyCheckResponse", names);
        Assert.Contains("ServiceOutcome", names);
        Assert.DoesNotContain(names, n => teachingData.Any(t => n.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void WebAssembly_ExposesNoDbContextSubclass()
    {
        var webTypes = typeof(ClassroomAgent.Web.Program).Assembly.GetTypes();

        Assert.DoesNotContain(webTypes, t => t.BaseType?.FullName == "Microsoft.EntityFrameworkCore.DbContext");
    }

    private static IReadOnlyList<string> ProjectReferences(string project) =>
        XDocument.Load(ProjectFile(project))
            .Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension((string?)r.Attribute("Include") ?? string.Empty))
            .ToList();

    private static string ProjectFile(string project) =>
        Path.Combine(StaticFiles.RepositoryRoot(), "src", project, project + ".csproj");
}
