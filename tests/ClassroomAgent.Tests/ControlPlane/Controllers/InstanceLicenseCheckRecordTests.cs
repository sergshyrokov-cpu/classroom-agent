using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Controllers;

/// <summary>
/// US-005 AC-005, AC-006, AC-015: every call of a known installation replaces its one
/// <c>instance_license_check</c> record, including under concurrency; checks are logged with identifiers
/// only and never audited (spec FR-006, FR-012, I-10, I-14; db-design §3.2; test strategy §3 event names).
/// </summary>
public sealed class InstanceLicenseCheckRecordTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task FirstCall_CreatesOneRecord_WithAnswerTimeVersionsAndAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var installationId = await host.InstallationInternalIdAsync(identifier, ct);
        host.Time.Advance(TimeSpan.FromMinutes(3));
        var now = host.Time.GetUtcNow();

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: "1.2.3", contractVersion: 1);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        var row = Assert.Single(await host.InstanceLicenseChecksAsync(ct));
        Assert.Equal(installationId, row.InstallationId);
        Assert.Equal(now, row.AnsweredAt);
        Assert.Equal("1.2.3", row.ApplicationVersion);
        Assert.Equal(1, row.ContractVersion);
        Assert.Equal("active", row.AnsweredStatus);
        Assert.Equal("supported", row.AnsweredCompatibility);
        Assert.Equal(now, row.CreatedAt);
        Assert.Equal(now, row.UpdatedAt);
    }

    [Fact]
    public async Task SecondCall_ReplacesTheRecord_CreatedAtKept()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new Dictionary<string, string> { [InstallationConfigurationKeys.RecommendedVersion] = "2.0.0" };
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct, extraSettings: settings);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        var first = host.Time.GetUtcNow();
        await host.PostCheckAsync(identifier, ct, applicationVersion: "2.0.0");
        host.Time.Advance(TimeSpan.FromHours(6));
        await host.SetInstallationStatusAsync(identifier, "suspended", ct);
        var second = host.Time.GetUtcNow();

        await host.PostCheckAsync(identifier, ct, applicationVersion: "1.9.0", contractVersion: 1);

        var row = Assert.Single(await host.InstanceLicenseChecksAsync(ct));
        Assert.Equal(second, row.AnsweredAt);
        Assert.Equal("1.9.0", row.ApplicationVersion);
        Assert.Equal("suspended", row.AnsweredStatus);
        Assert.Equal("upgrade_recommended", row.AnsweredCompatibility);
        Assert.Equal(first, row.CreatedAt);
        Assert.Equal(second, row.UpdatedAt);
    }

    [Fact]
    public async Task UpgradeRequiredAnswer_IsRecorded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        await host.PostCheckAsync(identifier, ct, contractVersion: 7);

        var row = Assert.Single(await host.InstanceLicenseChecksAsync(ct));
        Assert.Equal(7, row.ContractVersion);
        Assert.Equal("upgrade_required", row.AnsweredCompatibility);
    }

    [Fact]
    public async Task EachInstallation_HasItsOwnRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var one = await host.InsertInstallationAsync(ct);
        var two = await host.InsertInstallationAsync(
            ct,
            name: InstallationTestData.OtherName,
            domain: InstallationTestData.OtherDomain,
            clientId: InstallationTestData.OtherClientId);

        var oneId = await host.InstallationInternalIdAsync(one, ct);
        var twoId = await host.InstallationInternalIdAsync(two, ct);

        await host.PostCheckAsync(one, ct, applicationVersion: "1.0.0");
        await host.PostCheckAsync(two, ct, applicationVersion: "2.0.0");
        await host.PostCheckAsync(one, ct, applicationVersion: "1.0.1");

        var rows = await host.InstanceLicenseChecksAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.Equal("1.0.1", rows.Single(r => r.InstallationId == oneId).ApplicationVersion);
        Assert.Equal("2.0.0", rows.Single(r => r.InstallationId == twoId).ApplicationVersion);
    }

    [Fact]
    public async Task ConcurrentFirstCalls_LeaveOneRecord_AllAnswered200()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);

        for (var round = 0; round < 3; round++)
        {
            var identifier = await host.InsertInstallationAsync(
                ct,
                domain: $"round{round}.example.test",
                clientId: $"30000000000000000000{round}");
            var versions = Enumerable.Range(0, 8).Select(i => $"1.0.{i}").ToList();

            var responses = await Task.WhenAll(versions.Select(v => host.PostCheckAsync(identifier, ct, applicationVersion: v)));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.Status));
            var installationId = await host.InstallationInternalIdAsync(identifier, ct);
            var row = Assert.Single(await host.InstanceLicenseChecksAsync(ct), r => r.InstallationId == installationId);
            Assert.Contains(row.ApplicationVersion, versions);
        }
    }

    [Fact]
    public async Task Answer_DoesNotEchoVersionsOrIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);

        var response = await host.PostCheckAsync(identifier, ct, applicationVersion: "4.5.6");

        Assert.DoesNotContain("4.5.6", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(identifier.ToString("D"), response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KnownCall_IsLoggedAtInformation_UnknownAtWarning_WithoutDomainClientIdOrBody()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        var identifier = await host.InsertInstallationAsync(ct);
        const string bodyMarker = "zz-rejected-body-marker";

        await host.PostCheckAsync(identifier, ct, applicationVersion: "3.1.4");
        await host.PostCheckAsync(identifier, ct, applicationVersion: "3.1.5");
        await host.PostCheckAsync(Guid.Parse(InstallationTestData.UnknownIdentifier), ct);
        await host.PostCheckAsync($$"""{"installationId":"{{identifier:D}}","applicationVersion":"{{bodyMarker}}","contractVersion":1}""", ct);
        var events = await host.ReadLogEventsAsync(ct);

        var answered = events.Where(e => e.EventName == "LegitimacyCheckAnswered").ToList();
        Assert.Equal(2, answered.Count);
        Assert.All(answered, e => Assert.Equal("Information", e.Level));
        Assert.Contains(answered, e => e.Line.Contains("3.1.4", StringComparison.Ordinal));
        var unknown = Assert.Single(events, e => e.EventName == "LegitimacyCheckUnknownInstallation");
        Assert.Equal("Warning", unknown.Level);
        Assert.Contains(InstallationTestData.UnknownIdentifier, unknown.Line, StringComparison.OrdinalIgnoreCase);

        var all = string.Join('\n', events.Select(e => e.Line));
        Assert.DoesNotContain(InstallationTestData.Domain, all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationTestData.ClientId, all, StringComparison.Ordinal);
        Assert.DoesNotContain(bodyMarker, all, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallationTestData.Name, all, StringComparison.Ordinal);
    }
}
