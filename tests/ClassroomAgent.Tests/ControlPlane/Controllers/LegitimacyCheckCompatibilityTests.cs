using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-005 AC-004: the Control Plane decides compatibility from the contract versions it supports and the
/// optional minimum supported and recommended versions in its configuration (spec FR-005, I-7, I-8; VR-004).
/// </summary>
public sealed class LegitimacyCheckCompatibilityTests(PostgreSqlFixture database)
{
    private static readonly Dictionary<string, string> BothVersions = new()
    {
        [InstallationConfigurationKeys.MinimumSupportedVersion] = "1.2.0",
        [InstallationConfigurationKeys.RecommendedVersion] = "1.4.0",
    };

    [Theory]
    [InlineData("0.9.9", "upgrade_required")]
    [InlineData("1.1.99", "upgrade_required")]
    [InlineData("1.2.0", "upgrade_recommended")]
    [InlineData("1.3.999", "upgrade_recommended")]
    [InlineData("1.4.0", "supported")]
    [InlineData("1.4.1", "supported")]
    [InlineData("1.10.0", "supported")]
    [InlineData("2.0.0", "supported")]
    public async Task WithMinimumAndRecommended_VersionsCompareNumerically(string applicationVersion, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct, extraSettings: BothVersions);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: applicationVersion);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(expected, LegitimacyCheckHostExtensions.JsonProperties(response)["compatibility"]);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(999999)]
    public async Task UnsupportedContractVersion_IsUpgradeRequired_EvenWithANewApplicationVersion(int contractVersion)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct, extraSettings: BothVersions);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: "9.9.9", contractVersion: contractVersion);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("upgrade_required", LegitimacyCheckHostExtensions.JsonProperties(response)["compatibility"]);
    }

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("0.0.1")]
    [InlineData("999999.0.0")]
    public async Task WithoutVersionSettings_ContractVersionOne_IsSupported(string applicationVersion)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: applicationVersion);

        Assert.Equal("supported", LegitimacyCheckHostExtensions.JsonProperties(response)["compatibility"]);
    }

    [Fact]
    public async Task WithoutVersionSettings_UnsupportedContractVersion_IsUpgradeRequired()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, contractVersion: 2);

        Assert.Equal("upgrade_required", LegitimacyCheckHostExtensions.JsonProperties(response)["compatibility"]);
    }

    [Theory]
    [InlineData("0.9.0", "upgrade_recommended")]
    [InlineData("1.0.0", "supported")]
    public async Task OnlyRecommendedSet_ImposesNoMinimum(string applicationVersion, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new Dictionary<string, string> { [InstallationConfigurationKeys.RecommendedVersion] = "1.0.0" };
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct, extraSettings: settings);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: applicationVersion);

        Assert.Equal(expected, LegitimacyCheckHostExtensions.JsonProperties(response)["compatibility"]);
    }

    [Theory]
    [InlineData("0.9.0", "upgrade_required")]
    [InlineData("1.0.0", "supported")]
    public async Task OnlyMinimumSet_ImposesNoRecommendation(string applicationVersion, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new Dictionary<string, string> { [InstallationConfigurationKeys.MinimumSupportedVersion] = "1.0.0" };
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct, extraSettings: settings);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: applicationVersion);

        Assert.Equal(expected, LegitimacyCheckHostExtensions.JsonProperties(response)["compatibility"]);
    }

    [Theory]
    [InlineData("1.0.0", "upgrade_required")]
    [InlineData("1.5.0", "supported")]
    public async Task RecommendedBelowMinimum_IsAccepted_AndNeverApplies(string applicationVersion, string expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new Dictionary<string, string>
        {
            [InstallationConfigurationKeys.MinimumSupportedVersion] = "1.5.0",
            [InstallationConfigurationKeys.RecommendedVersion] = "1.2.0",
        };
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct, extraSettings: settings);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: applicationVersion);

        Assert.Equal(expected, LegitimacyCheckHostExtensions.JsonProperties(response)["compatibility"]);
    }

    [Fact]
    public async Task SuspendedInstallation_StillGetsItsCompatibilityComputed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct, extraSettings: BothVersions);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct, status: "suspended");

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: "1.3.0");

        var body = LegitimacyCheckHostExtensions.JsonProperties(response);
        Assert.Equal("suspended", body["status"]);
        Assert.Equal("upgrade_recommended", body["compatibility"]);
    }

    [Theory]
    [InlineData(InstallationConfigurationKeys.MinimumSupportedVersion, "1.2")]
    [InlineData(InstallationConfigurationKeys.MinimumSupportedVersion, "v1.2.0")]
    [InlineData(InstallationConfigurationKeys.RecommendedVersion, "1.2.0-beta")]
    [InlineData(InstallationConfigurationKeys.RecommendedVersion, "latest")]
    public async Task InvalidVersionSetting_StopsTheControlPlaneAtStartup_NamingTheKeyNotTheValue(string key, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new Dictionary<string, string> { [key] = value };

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var host = await ControlPlaneTestHost.StartAsync(database, ct, extraSettings: settings);
        });

        var text = ExceptionText(exception);
        Assert.Contains(key, text, StringComparison.Ordinal);
        Assert.DoesNotContain(value, text, StringComparison.Ordinal);
    }

    private static string ExceptionText(Exception exception)
    {
        var parts = new List<string>();
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            parts.Add(e.Message);
            if (e is AggregateException aggregate)
            {
                parts.AddRange(aggregate.InnerExceptions.Select(i => i.Message));
            }
        }

        return string.Join(Environment.NewLine, parts);
    }
}
