using System.Xml.Linq;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-047 AC-008 (spec FR-008, AD-3): <c>Microsoft.Extensions.Hosting.WindowsServices</c> is referenced by the two
/// hosts only, on the .NET 10 line, and the Story adds no other package anywhere.
/// </summary>
public sealed class WindowsServicePackageTests
{
    private const string WindowsServicesPackage = "Microsoft.Extensions.Hosting.WindowsServices";

    /// <summary>Every production project's packages after US-047: the earlier ones plus the approved one in the hosts.</summary>
    public static TheoryData<string, string[]> ApprovedPackages => new()
    {
        { "ClassroomAgent.Domain", [] },
        { "ClassroomAgent.Contracts", [] },
        { "ClassroomAgent.Application", [] },
        {
            "ClassroomAgent.Infrastructure",
            [
                "ClosedXML",
                "EFCore.NamingConventions",
                "Google.Apis.Admin.Reports.reports_v1",
                "Google.Apis.Auth",
                "Google.Apis.Classroom.v1",
                "Microsoft.Extensions.Identity.Core",
                "Npgsql.EntityFrameworkCore.PostgreSQL",
            ]
        },
        {
            "ClassroomAgent.Web",
            [
                "Microsoft.AspNetCore.Authentication.Google",
                "Microsoft.EntityFrameworkCore.Design",
                WindowsServicesPackage,
                "Serilog.AspNetCore",
                "Serilog.Sinks.File",
            ]
        },
        {
            "ClassroomAgent.ControlPlane",
            [
                "EFCore.NamingConventions",
                "Microsoft.EntityFrameworkCore.Design",
                WindowsServicesPackage,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                "Serilog.AspNetCore",
                "Serilog.Sinks.File",
            ]
        },
    };

    [Theory]
    [MemberData(nameof(ApprovedPackages))]
    public void ProjectPackages_AreExactlyTheApprovedOnes(string project, string[] approved)
    {
        Assert.Equal(approved.Order(StringComparer.Ordinal), PackageReferences(project).Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("ClassroomAgent.Web")]
    [InlineData("ClassroomAgent.ControlPlane")]
    public void WindowsServicesPackage_FollowsTheDotNet10Line(string project)
    {
        var version = PackageReferences(project)[WindowsServicesPackage];

        Assert.StartsWith("10.", version, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> PackageReferences(string project) =>
        XDocument.Load(Path.Combine(StaticFiles.RepositoryRoot(), "src", project, project + ".csproj"))
            .Descendants("PackageReference")
            .ToDictionary(
                reference => (string?)reference.Attribute("Include") ?? string.Empty,
                reference => (string?)reference.Attribute("Version") ?? string.Empty,
                StringComparer.Ordinal);
}
