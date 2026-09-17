using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.ControlPlane;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.ControlPlane;

/// <summary>
/// US-005 AC-003, AC-006: both sides of the channel agree on the contract — the installation's real client
/// against the in-process Control Plane (api-design §4 … §6). No network: the client's handler is the
/// Control Plane test server.
/// </summary>
public sealed class LegitimacyChannelContractTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task KnownInstallation_IsAnAnswer_AndTheControlPlaneRecordsTheCall()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var controlPlane = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await controlPlane.CreateOwnerAsync(ct);
        var identifier = await controlPlane.InsertInstallationAsync(ct, status: "suspended");

        var reply = await ClientFor(controlPlane).CheckAsync(identifier, "3.2.1", 1, ct);

        Assert.Equal(
            new ControlPlaneCheckReply.Answer(InstallationStatus.Suspended, CompatibilityState.Supported, InstallationTestData.Domain, InstallationTestData.ClientId),
            reply);
        var row = Assert.Single(await controlPlane.InstanceLicenseChecksAsync(ct));
        Assert.Equal("3.2.1", row.ApplicationVersion);
    }

    [Fact]
    public async Task UnsupportedContractVersion_IsAnAnswerWithUpgradeRequired()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var controlPlane = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await controlPlane.CreateOwnerAsync(ct);
        var identifier = await controlPlane.InsertInstallationAsync(ct);

        var reply = await ClientFor(controlPlane).CheckAsync(identifier, "1.0.0", 2, ct);

        var answer = Assert.IsType<ControlPlaneCheckReply.Answer>(reply);
        Assert.Equal(CompatibilityState.UpgradeRequired, answer.Compatibility);
    }

    [Fact]
    public async Task UnknownInstallation_IsUnknownInstallation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var controlPlane = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await controlPlane.CreateOwnerAsync(ct);
        await controlPlane.InsertInstallationAsync(ct);

        var reply = await ClientFor(controlPlane).CheckAsync(Guid.Parse(InstallationTestData.UnknownIdentifier), "1.0.0", 1, ct);

        Assert.Equal(new ControlPlaneCheckReply.Failure(CheckFailureCategory.UnknownInstallation), reply);
    }

    [Fact]
    public async Task ControlPlaneBeforeSetup_RedirectIsAnErrorAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var controlPlane = await ControlPlaneTestHost.StartAsync(database, ct);
        var identifier = await controlPlane.InsertInstallationAsync(ct);

        var reply = await ClientFor(controlPlane).CheckAsync(identifier, "1.0.0", 1, ct);

        Assert.Equal(new ControlPlaneCheckReply.Failure(CheckFailureCategory.ErrorAnswer), reply);
        Assert.Empty(await controlPlane.InstanceLicenseChecksAsync(ct));
    }

    private static ControlPlaneClient ClientFor(ControlPlaneTestHost controlPlane) =>
        new(
            new HttpClient(controlPlane.CreateServerHandler()) { BaseAddress = new Uri("https://localhost/") },
            new ManualTimeProvider(InstallationTestHost.DefaultStart));
}
